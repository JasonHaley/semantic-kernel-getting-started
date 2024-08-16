using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Text;

namespace PropertyGraph.Common;
public class TripletsExtractor
{
    private readonly IAppOptions _options;
    private readonly ILogger _logger;

    public TripletsExtractor(IAppOptions options)
    {
        _options = options;
        _logger = _options.LoggerFactory.CreateLogger(nameof(TripletsExtractor));
    }

    public async Task<string> ExtractAsync(string fileName)
    {
        DocumentMetadata documentMetatdata = new(Utilities.CreateId(fileName), fileName);
        var chunks = await ExtractEntitiesFromDocumentChunksAsync(documentMetatdata);
        var entities = DeduplicateEntities(chunks);

        var cypherText = CreateCypherText(documentMetatdata, chunks, entities);
                
        return cypherText;
    }

    public async Task<Dictionary<ChunkMetadata, List<TripletRow>>> ExtractEntitiesFromDocumentChunksAsync(DocumentMetadata documentMetatdata)
    {
        Dictionary<ChunkMetadata, List<TripletRow>> chunks = new Dictionary<ChunkMetadata, List<TripletRow>>();

        if (!File.Exists(documentMetatdata.source))
        {
            _logger.LogInformation($"File '{documentMetatdata.source} not found");
            return chunks;
        }

        List<string> chunkTextList = SplitDocumentIntoChunks(documentMetatdata);

        var prompts = _options.Kernel.CreatePluginFromPromptDirectory("Prompts");

        for (int i = 0; i < chunkTextList.Count; i++)
        {
            string text = chunkTextList[i];
            string currentDocumentChunk = $"DocumentChunk{i}";
            string id = Utilities.CreateId($"{currentDocumentChunk}{documentMetatdata.id}");

            ChunkMetadata chunkMetadata = new (id, currentDocumentChunk, i, documentMetatdata.id, text);

            var result = await _options.Kernel.InvokePromptAsync<List<TripletRow>>(
                prompts["ExtractEntities"],
                new() {
                    { "maxTripletsPerChunk", _options.PropertyGraph.MaxTripletsPerChunk ?? Defaults.MAX_TRIPLETS_PER_CHUNK },
                    { "preamble", _options.PropertyGraph.EntityExtractonTemplatePreamble ?? string.Empty },
                    { "entityTypes", _options.PropertyGraph.EntityTypes ?? Defaults.ENTITY_TYPES },
                    { "relationTypes", _options.PropertyGraph.RelationshipTypes ?? Defaults.RELATION_TYPES },
                    { "text", text },
                });

            if (result != null)
            {
                if (result != null && result.Count > 0)
                {
                    chunks.Add(chunkMetadata, result);
                }
            }
            else
            {
                _logger.LogWarning("ExtractEntities prompt invoke returned null");
            }
        }

        _logger.LogInformation($"Number of chunks: {chunks.Count}");

        return chunks;
    }

    private List<string> SplitDocumentIntoChunks(DocumentMetadata documentMetatdata)
    {
        List<string> paragraphs;

        if (_options.PropertyGraph.UseTokenSplitter)
        {
            var tokenizer = TiktokenTokenizer.CreateForModel(_options.OpenAI.ChatModelId);
            string fileText = File.ReadAllText(documentMetatdata.source);

            var lines = TextChunker.SplitPlainTextLines(fileText, _options.PropertyGraph.ChunkSize ?? Defaults.CHUNK_SIZE, text => tokenizer.CountTokens(text));
            paragraphs = TextChunker.SplitPlainTextParagraphs(lines, _options.PropertyGraph.ChunkSize ?? Defaults.CHUNK_SIZE,
                                _options.PropertyGraph.Overlap ?? Defaults.OVERLAP, null, text => tokenizer.CountTokens(text));
        }
        else
        {
            var simpleLines = File.ReadAllLines(documentMetatdata.source);
            paragraphs = Utilities.SplitPlainTextOnEmptyLine(simpleLines);
        }

        return paragraphs;
    }

    private Dictionary<string, EntityMetadata> DeduplicateEntities(Dictionary<ChunkMetadata, List<TripletRow>> chunks)
    {
        Dictionary<string, EntityMetadata> entities = new Dictionary<string, EntityMetadata>();

        foreach (ChunkMetadata key in chunks.Keys)
        {
            List<TripletRow> triplets = chunks[key];
            foreach (var triplet in triplets)
            {
                EntityMetadata entity;
                string pcHead = Utilities.CreateName(triplet.head);
                if (entities.ContainsKey(pcHead))
                {
                    entity = entities[pcHead];
                    if (!entity.mentionedInChunks.ContainsKey(key.id))
                    {
                        entity.mentionedInChunks.Add(key.id, key);
                    }
                }
                else
                {
                    entity = new EntityMetadata();
                    entities.Add(pcHead, Utilities.PopulateEntityMetadata(key, triplet, entity, true));
                }

                string pcTail = Utilities.CreateName(triplet.tail);
                if (entities.ContainsKey(pcTail))
                {
                    entity = entities[pcTail];
                    if (!entity.mentionedInChunks.ContainsKey(key.id))
                    {
                        entity.mentionedInChunks.Add(key.id, key);
                    }
                }
                else
                {
                    entity = new EntityMetadata();
                    entities.Add(pcTail, Utilities.PopulateEntityMetadata(key, triplet, entity, false));
                }
            }
        }

        _logger.LogInformation($"Unique entity count: {entities.Count}");
        
        // for logging
        foreach (var key in entities.Keys)
        {
            var e = entities[key];
            _logger.LogTrace($"{key} Mentioned In {e.mentionedInChunks.Count} chunks");
        }

        return entities;
    }

    private string CreateCypherText(DocumentMetadata documentMetadata, Dictionary<ChunkMetadata, List<TripletRow>> chunks, Dictionary<string, EntityMetadata> entities)
    {
        List<string> entityCypherText = new List<string>();

        entityCypherText.Add($"MERGE (Document1:DOCUMENT {{ id: '{documentMetadata.id}', name:'Document1', type:'DOCUMENT', source: '{documentMetadata.source}'}})");

        string documentChunkType = string.IsNullOrEmpty(_options.PropertyGraph.DocumentChunkTypeLabel) ? Defaults.DOCUMENT_CHUNK_TYPE : _options.PropertyGraph.DocumentChunkTypeLabel;

        foreach (var chunk in chunks.Keys)
        {
            entityCypherText.Add($"MERGE (DocumentChunk{chunk.sequence}:DOCUMENT_CHUNK {{ id: '{chunk.id}', name: '{chunk.name}', type: '{documentChunkType}', documentId: '{chunk.documentId}', source: '{documentMetadata.source}', sequence: '{chunk.sequence}', text: \"{chunk.text.Replace("\"", "'")}\"}})");
            entityCypherText.Add($"MERGE (Document1)-[:CONTAINS]->(DocumentChunk{chunk.sequence})");
        }

        HashSet<string> types = new HashSet<string>();
        foreach (var entity in entities.Keys)
        {
            var labels = entities[entity];
            var pcEntity = entity;
            
            // Handle strange issue when type is empty string
            if (string.IsNullOrEmpty(labels.type))
            {
                continue;
            }

            entityCypherText.Add($"MERGE ({pcEntity}:ENTITY {{ name: '{pcEntity}', type: '{labels.type}', id: '{labels.id}', documentId: '{documentMetadata.id}', source: '{documentMetadata.source}', text: '{labels.text}'}})");

            if (!types.Contains(labels.type))
            {
                types.Add(labels.type);
            }

            foreach (var key in labels.mentionedInChunks.Keys)
            {
                var documentChunk = labels.mentionedInChunks[key];
                entityCypherText.Add($"MERGE ({pcEntity})-[:MENTIONED_IN]->(DocumentChunk{documentChunk.sequence})");
            }
        }

        HashSet<string> relationships = new HashSet<string>();
        foreach (ChunkMetadata key in chunks.Keys)
        {
            List<TripletRow> triplets = chunks[key];
            foreach (var triplet in triplets)
            {
                var pcHead = Utilities.CreateName(triplet.head);
                var pcTail = Utilities.CreateName(triplet.tail);
                var relationName = triplet.relation.Replace(" ", "_").Replace("-", "_");
                if (string.IsNullOrEmpty(relationName))
                {
                    relationName = "RELATED_TO";
                }
                entityCypherText.Add($"MERGE ({pcHead})-[:{relationName}]->({pcTail})");

                string headRelationship = $"MERGE (DocumentChunk{key.sequence})-[:MENTIONS]->({pcHead})";
                if (!relationships.Contains(headRelationship))
                {
                    relationships.Add(headRelationship);
                    entityCypherText.Add(headRelationship);
                }

                string tailRelationship = $"MERGE (DocumentChunk{key.sequence})-[:MENTIONS]->({pcTail})";
                if (!relationships.Contains(tailRelationship))
                {
                    relationships.Add(tailRelationship);
                    entityCypherText.Add(tailRelationship);
                }
            }
        }

        // For logging
        foreach (var t in entityCypherText)
        {
            _logger.LogTrace(t);
        }

        StringBuilder all = new StringBuilder();
        all.AppendJoin(Environment.NewLine, entityCypherText.ToArray());

        return all.ToString();
    }
}

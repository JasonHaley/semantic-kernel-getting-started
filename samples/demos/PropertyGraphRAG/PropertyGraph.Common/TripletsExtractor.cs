using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel;
using System.Text.Json;
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

        var tokenizer = TiktokenTokenizer.CreateForModel(_options.OpenAI.ChatModelId);

        if (!File.Exists(documentMetatdata.source))
        {
            _logger.LogInformation($"File '{documentMetatdata.source} not found");
            return chunks;
        }

        List<string> paragraphs;
        
        if (_options.PropertyGraph.UseTokenSplitter)
        {
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
        
        var prompts = _options.Kernel.CreatePluginFromPromptDirectory("Prompts");

        for (int i = 0; i < paragraphs.Count; i++)
        {
            string text = paragraphs[i];
                        
            ChunkMetadata chunkMetadata = new(Utilities.CreateId($"DocumentChunk{i}{documentMetatdata.id}"), $"DocumentChunk{i}", i, documentMetatdata.id, text);

            var result = await _options.Kernel.InvokeAsync(
                prompts["ExtractEntities"],
                new() {
                    { "maxTripletsPerChunk", _options.PropertyGraph.MaxTripletsPerChunk ?? Defaults.MAX_TRIPLETS_PER_CHUNK },
                    { "preamble", _options.PropertyGraph.EntityExtractonTemplatePreamble ?? string.Empty },
                    { "entityTypes", _options.PropertyGraph.EntityTypes ?? Defaults.ENTITY_TYPES },
                    { "relationTypes", _options.PropertyGraph.RelationshipTypes ?? Defaults.RELATION_TYPES },
                    { "text", text },
                });

            _logger.LogTrace(result.ToString());

            if (result != null)
            {
                // TODO: Write a utility to do this cleanup
                string jsonText = result.ToString();
                List<TripletRow>? rows = JsonSerializer.Deserialize<List<TripletRow>>(jsonText.Replace("```json", "").Replace("```", "").Replace("'", "").Trim());

                if (rows != null && rows.Count > 0)
                {
                    chunks.Add(chunkMetadata, rows);
                }
            }
        }

        _logger.LogInformation($"Number of chunks: {chunks.Count}");
        
        return chunks;
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

        string documentChunkType = _options.PropertyGraph.DocumentChunkTypeLabel ?? Defaults.DOCUMENT_CHUNK_TYPE;

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
                entityCypherText.Add($"MERGE ({pcHead})-[:{triplet.relation.Replace(" ", "_").Replace("-", "_")}]->({pcTail})");

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

        foreach (var t in entityCypherText)
        {
            _logger.LogTrace(t);
        }

        StringBuilder all = new StringBuilder();
        all.AppendJoin(Environment.NewLine, entityCypherText.ToArray());

        return all.ToString();
    }


}

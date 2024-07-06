// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using SemanticKernel.Data.Nl2Sql.Library.Internal;
using SemanticKernel.Data.Nl2Sql.Library.Schema;

namespace SemanticKernel.Data.Nl2Sql.Library;

/// <summary>
/// Generate SQL query targeting Microsoft SQL Server.
/// </summary>
public sealed class SqlQueryGenerator
{
    public const double DefaultMinRelevance = 0.7D;

    public const string ContextParamObjective = "data_objective";
    public const string ContextParamSchema = "data_schema";
    public const string ContextParamSchemaId = "data_schema_id";
    public const string ContextParamQuery = "data_query";
    public const string ContextParamPlatform = "data_platform";
    public const string ContextParamResult = "data_result";
    public const string ContextParamError = "data_error";
    
    private const string ContentLabelQuery = "sql";
    private const string ContentLabelAnswer = "answer";
    private const string ContentAffirmative = "yes";

    private readonly double _minRelevanceScore;
    private readonly KernelFunction _promptEval;
    private readonly KernelFunction _promptGenerator;
    private readonly KernelFunction _promptResultEval;
    private readonly KernelFunction _promptDescribeResults;
    private readonly Kernel _kernel;
    private readonly ISemanticTextMemory _memory;

    public SqlQueryGenerator(
        Kernel kernel,
        ISemanticTextMemory memory,
        double minRelevanceScore = DefaultMinRelevance)
    {
        var prompts = kernel.CreatePluginFromPromptDirectory("Prompts");

        this._promptEval = prompts["EvaluateIntent"];
        this._promptGenerator = prompts["SqlGenerate"];
        this._promptResultEval = prompts["EvaluateResult"];
        this._promptDescribeResults = prompts["DescribeResults"];
        this._kernel = kernel;
        this._memory = memory;
        this._minRelevanceScore = minRelevanceScore;
    }

    /// <summary>
    /// Attempt to produce a query for the given objective based on the registered schemas.
    /// </summary>
    /// <param name="objective">A natural language objective</param>
    /// <param name="context">A <see cref="SKContext"/> object</param>
    /// <returns>A SQL query (or null if not able)</returns>
    public async Task<SqlQueryResult?> SolveObjectiveAsync(string objective)
    {
        // Search for schema with best similarity match to the objective
        var recall =
            await this._memory.SearchAsync(
                SchemaProvider.MemoryCollectionName,
                objective,
                limit: 1, // Take top result with maximum relevance (> minRelevanceScore)
                minRelevanceScore: this._minRelevanceScore,
                withEmbeddings: true).ToArrayAsync().ConfigureAwait(false);

        var best = recall.FirstOrDefault();
        if (best == null)
        {
            return null; // No schema / no query
        }

        var schemaName = best.Metadata.Id;
        var schemaText = best.Metadata.Text;
        
        var arguments = new KernelArguments();
        arguments[ContextParamSchema] = schemaText;
        arguments[ContextParamObjective] = objective;
        
        // Screen objective to determine if it can be solved with the selected schema.
        if (!await this.ScreenObjectiveAsync(arguments).ConfigureAwait(false))
        {
            return null; // Objective doesn't pass screen
        }

        var sqlPlatform = best.Metadata.AdditionalMetadata;
        arguments[ContextParamPlatform] = sqlPlatform;

        // Generate query
        var result = await this._promptGenerator.InvokeAsync(this._kernel, arguments).ConfigureAwait(false);

        // Parse result to handle 
        string query = result.ParseValue(ContentLabelQuery);

        return new SqlQueryResult(schemaName, query);
    }

    public async Task<string> ProcessResultAsync(string objective, string query, List<List<string>> dataResult)
    {
        var arguments = new KernelArguments();
        arguments[ContextParamObjective] = objective;
        arguments[ContextParamQuery] = query;
        arguments[ContextParamResult] = JsonSerializer.Serialize(dataResult);

        
        // Generate query
        var result = await this._promptResultEval.InvokeAsync(this._kernel, arguments).ConfigureAwait(false);
        var answer = result.ToString();

        return answer;
    }

    public async Task<string> DescribeResultsAsync(List<List<string>> dataResult)
    {
        var arguments = new KernelArguments();
        arguments[ContextParamResult] = JsonSerializer.Serialize(dataResult.Take(10));


        // Generate query
        var result = await this._promptDescribeResults.InvokeAsync(this._kernel, arguments).ConfigureAwait(false);
        var answer = result.ToString();

        return answer;
    }

    private async Task<bool> ScreenObjectiveAsync(KernelArguments context)
    {
        var result = await this._promptEval.InvokeAsync(this._kernel, context).ConfigureAwait(false);

        var answer = result.ParseValue(ContentLabelAnswer);
        return answer.Equals(ContentAffirmative, StringComparison.OrdinalIgnoreCase);
    }
}

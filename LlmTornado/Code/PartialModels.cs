﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LlmTornado.Images;
using LlmTornado;
using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Code;

public class Ref<T>
{
    public T? Ptr { get; set; }
}

internal class StreamResponse
{
    public Stream Stream { get; set; }
    public ApiResultBase Headers { get; set; }
    public HttpResponseMessage Response { get; set; }
}

/// <summary>
///     A failed HTTP request.
/// </summary>
public class HttpFailedRequest
{
    /// <summary>
    ///     The exception with details what went wrong.
    /// </summary>
    public Exception Exception { get; set; }
    
    /// <summary>
    ///     The request that failed.
    /// </summary>
    public HttpCallRequest? Request { get; set; }
    
    /// <summary>
    ///     Result of the failed request.
    /// </summary>
    public IHttpCallResult? Result { get; set; }
    
    /// <summary>
    ///     Raw message of the failed request. Do not dispose this, it will be disposed automatically by Tornado.
    /// </summary>
    public HttpResponseMessage RawMessage { get; set; }
}

public class StreamRequest : IAsyncDisposable
{
    public Stream Stream { get; set; }
    public HttpResponseMessage Response { get; set; }
    public StreamReader StreamReader { get; set; }
    public Exception? Exception { get; set; }
    public HttpCallRequest? CallRequest { get; set; }
    public IHttpCallResult? CallResponse { get; set; }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
        Response.Dispose();
        StreamReader.Dispose();
    }
}

/// <summary>
///     Roles of chat participants.
/// </summary>
public enum ChatMessageRoles
{
    /// <summary>
    ///     Unknown role.
    /// </summary>
    Unknown,
    /// <summary>
    ///     System prompt / preamble.
    /// </summary>
    System,
    /// <summary>
    ///     Messages written by user.
    /// </summary>
    User,
    /// <summary>
    ///     Assistant messages.
    /// </summary>
    Assistant,
    /// <summary>
    ///     Messages representing tool/function/connector usage.
    /// </summary>
    Tool
}

internal enum ChatResultStreamInternalKinds
{
    Unknown,
    None,
    AppendAssistantMessage
}

public class ChatFunctionParamsGetter
{
    private readonly Dictionary<string, object?>? source;

    public ChatFunctionParamsGetter(Dictionary<string, object?>? pars)
    {
        source = pars;
    }

    public bool Get<T>(string param, [NotNullWhen(returnValue: true)] out T? data, out Exception? exception)
    {
        exception = null;
        
        if (source is null)
        {
            data = default;
            return false; 
        }
        
        if (!source.TryGetValue(param, out object? rawData))
        {
            data = default;
            return false;
        }

        if (rawData is T obj)
        {
            data = obj;
        }

        if (rawData is JArray jArr)
        {
            data = jArr.ToObject<T?>();
            return true;
        }

        try
        {
            data = (T?)rawData.ChangeType(typeof(T));
            return true;
        }
        catch (Exception e)
        {
            data = default;
            exception = e;
            return false;
        }
    }
}

internal class ToolCallInboundAccumulator
{
    public ToolCall ToolCall { get; set; }
    public StringBuilder ArgumentsBuilder { get; set; } = new StringBuilder();
}

/// <summary>
///     Represents a chat image
/// </summary>
public class ChatImage
{
    /// <summary>
    ///     Creates a new chat image
    /// </summary>
    /// <param name="content">Publicly available URL to the image or base64 encoded content</param>
    public ChatImage(string content)
    {
        Url = content;
    }

    /// <summary>
    ///     Creates a new chat image
    /// </summary>
    /// <param name="content">Publicly available URL to the image or base64 encoded content</param>
    /// <param name="detail">The detail level to use, defaults to <see cref="ImageDetail.Auto" /></param>
    public ChatImage(string content, ImageDetail? detail)
    {
        Url = content;
        Detail = detail;
    }

    /// <summary>
    ///     Publicly available URL to the image or base64 encoded content
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; }

    /// <summary>
    ///     Publicly available URL to the image or base64 encoded content
    /// </summary>
    [JsonProperty("detail")]
    public ImageDetail? Detail { get; set; }
}

/// <summary>
/// Known LLM providers.
/// </summary>
public enum LLmProviders
{
    /// <summary>
    /// Provider not resolved.
    /// </summary>
    Unknown,
    /// <summary>
    /// OpenAI.
    /// </summary>
    OpenAi,
    /// <summary>
    /// Anthropic.
    /// </summary>
    Anthropic,
    /// <summary>
    /// Azure OpenAI.
    /// </summary>
    AzureOpenAi,
    /// <summary>
    /// Cohere.
    /// </summary>
    Cohere,
    /// <summary>
    /// KoboldCpp, Ollama and other self-hosted providers.
    /// </summary>
    Custom,
    /// <summary>
    /// Internal value.
    /// </summary>
    Length
}

/// <summary>
/// 
/// </summary>
public enum CapabilityEndpoints
{
    Chat,
    Moderation,
    Completions,
    Embeddings,
    Models,
    Files,
    ImageGeneration,
    Audio,
    Assistants,
    ImageEdit,
    Threads,
    FineTuning
}

/// <summary>
/// Represents authentication to a single provider.
/// </summary>
public class ProviderAuthentication
{
    public LLmProviders Provider { get; set; }
    public string? ApiKey { get; set; }
    public string? Organization { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="providers"></param>
    /// <param name="ApiKey"></param>
    /// <param name="organization"></param>
    public ProviderAuthentication(LLmProviders provider, string apiKey, string? organization = null)
    {
        Provider = provider;
        ApiKey = apiKey;
        Organization = organization;
    }
}

/// <summary>
/// Types of inbound streams.
/// </summary>
public enum StreamRequestTypes
{
    /// <summary>
    /// Unrecognized stream.
    /// </summary>
    Unknown,
    /// <summary>
    /// Chat/completion stream.
    /// </summary>
    Chat
}

internal class StreamToken<T>
{
    public T? Data { get; set; }
    public bool Break { get; set; }

    public StreamToken(T? data, bool brk)
    {
        Data = data;
        Break = brk;
    } 
}

public class StreamChoicesBase
{
    
}
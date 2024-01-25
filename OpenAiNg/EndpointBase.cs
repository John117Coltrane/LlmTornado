﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenAiNg.Code;

namespace OpenAiNg;

/// <summary>
///     A base object for any OpenAI API endpoint, encompassing common functionality
/// </summary>
public abstract class EndpointBase
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/113.0.0.0 Safari/537.36";
    internal static readonly JsonSerializerSettings NullSettings = new() { NullValueHandling = NullValueHandling.Ignore };

    /// <summary>
    /// Gets the timeout for all http requests
    /// </summary>
    /// <returns></returns>
    public static int GetRequestsTimeout()
    {
        return (int)EndpointClient.Timeout.TotalSeconds;
    }

    /// <summary>
    /// Sets the timeout for all http requests
    /// </summary>
    /// <returns></returns>
    public static void SetRequestsTimeout(int seconds)
    {
        EndpointClient.Timeout = TimeSpan.FromSeconds(seconds);
    }

    
    private static readonly HttpClient EndpointClient = new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = 10000,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    }) {
        Timeout = TimeSpan.FromSeconds(600)
    };

    /// <summary>
    ///     Constructor of the api endpoint base, to be called from the contructor of any devived classes.  Rather than
    ///     instantiating any endpoint yourself, access it through an instance of <see cref="OpenAiApi" />.
    /// </summary>
    /// <param name="api"></param>
    internal EndpointBase(OpenAiApi api)
    {
        Api = api;
    }

    /// <summary>
    ///     The internal reference to the API, mostly used for authentication
    /// </summary>
    internal OpenAiApi Api { get; }

    /// <summary>
    ///     The name of the endpoint, which is the final path segment in the API URL.  Must be overriden in a derived class.
    /// </summary>
    protected abstract string Endpoint { get; }

    /// <summary>
    ///     Gets the URL of the endpoint, based on the base OpenAI API URL followed by the endpoint name.  For example
    ///     "https://api.openai.com/v1/completions"
    /// </summary>
    protected string Url => string.Format(Api.ApiUrlFormat, Api.ApiVersion, Endpoint);

    /// <summary>
    ///     Default max processing time of a http request in seconds
    /// </summary>
    public static void SetDefaultHttpTimeout(int timeoutSec)
    {
        EndpointClient.Timeout = TimeSpan.FromSeconds(timeoutSec);
    }

    /// <summary>
    ///     Gets an HTTPClient with the appropriate authorization and other headers set
    /// </summary>
    /// <returns>The fully initialized HttpClient</returns>
    /// <exception cref="AuthenticationException">
    ///     Thrown if there is no valid authentication.  Please refer to
    ///     <see href="https://github.com/OkGoDoIt/OpenAI-API-dotnet#authentication" /> for details.
    /// </exception>
    private static HttpClient GetClient() => EndpointClient;

    /// <summary>
    ///     Formats a human-readable error message relating to calling the API and parsing the response
    /// </summary>
    /// <param name="resultAsString">The full content returned in the http response</param>
    /// <param name="response">The http response object itself</param>
    /// <param name="name">The name of the endpoint being used</param>
    /// <param name="description">Additional details about the endpoint of this request (optional)</param>
    /// <param name="input">Additional details about the endpoint of this request (optional)</param>
    /// <returns>A human-readable string error message.</returns>
    private static string GetErrorMessage(string? resultAsString, HttpResponseMessage response, string name, string description, HttpRequestMessage input)
    {
        return $"Error at {name} ({description}) with HTTP status code: {response.StatusCode}. Content: {resultAsString ?? "<no content>"}. Request: {JsonConvert.SerializeObject(input.Headers)}";
    }

    /// <summary>
    ///     Sends an HTTP request and returns the response.  Does not do any parsing, but does do error handling.
    /// </summary>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request.  If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <param name="verb">
    ///     (optional) The HTTP verb to use, for example "<see cref="HttpMethod.Get" />".  If omitted, then
    ///     "GET" is assumed.
    /// </param>
    /// <param name="postData">(optional) A json-serializable object to include in the request body.</param>
    /// <param name="streaming">
    ///     (optional) If true, streams the response.  Otherwise waits for the entire response before
    ///     returning.
    /// </param>
    /// <returns>The HttpResponseMessage of the response, which is confirmed to be successful.</returns>
    /// <exception cref="HttpRequestException">Throws an exception if a non-success HTTP response was returned</exception>
    private async Task<HttpResponseMessage> HttpRequestRaw(string? url = null, HttpMethod? verb = null, object? postData = null, bool streaming = false)
    {
        url ??= Url;
        verb ??= HttpMethod.Get;

        HttpClient client = GetClient();
        using HttpRequestMessage req = new(verb, url);

        req.Headers.Add("User-Agent", UserAgent);
        
        if (Api.Auth is not null)
        {
            if (Api.Auth.ApiKey is not null)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Api.Auth.ApiKey);
                req.Headers.Add("api-key", Api.Auth.ApiKey);   
            }
            
            if (Api.Auth.Organization is not null)
            {
                req.Headers.Add("OpenAI-Organization", Api.Auth.Organization);
            }
        }

        if (postData != null)
        {
            if (postData is HttpContent data)
            {
                req.Content = data;
            }
            else
            {
                string jsonContent = JsonConvert.SerializeObject(postData, NullSettings);
                StringContent stringContent = new(jsonContent, Encoding.UTF8, "application/json");
                req.Content = stringContent;
            }
        }

        HttpResponseMessage response = await client.SendAsync(req, streaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);

        if (response.IsSuccessStatusCode) return response;

        string resultAsString;

        try
        {
            resultAsString = await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            resultAsString = $"Additionally, the following error was thrown when attemping to read the response content: {e}";
        }

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new AuthenticationException($"The API provider rejected your authorization, most likely due to an invalid API Key. Check your API Key and see https://github.com/lofcz/OpenAiNg#authentication for guidance. Full API response follows: {resultAsString}"),
            HttpStatusCode.InternalServerError => new HttpRequestException($"The API provider had an internal server error. Please retry your request. Server response: {GetErrorMessage(resultAsString, response, Endpoint, url, req)}"),
            _ => new HttpRequestException(GetErrorMessage(resultAsString, response, Endpoint, url, req))
        };
    }

    /// <summary>
    ///     Sends an HTTP Get request and return the string content of the response without parsing, and does error handling.
    /// </summary>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request.  If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <returns>The text string of the response, which is confirmed to be successful.</returns>
    /// <exception cref="HttpRequestException">Throws an exception if a non-success HTTP response was returned</exception>
    internal async Task<string> HttpGetContent(string? url = null)
    {
        using HttpResponseMessage response = await HttpRequestRaw(url);
        return await response.Content.ReadAsStringAsync();
    }
    
    /// <summary>
    ///     Sends an HTTP Request and does initial parsing
    /// </summary>
    /// <typeparam name="T">The <see cref="ApiResultBase" />-derived class for the result</typeparam>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request.  If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <param name="verb">
    ///     (optional) The HTTP verb to use, for example "<see cref="HttpMethod.Get" />".  If omitted, then
    ///     "GET" is assumed.
    /// </param>
    /// <param name="postData">(optional) A json-serializable object to include in the request body.</param>
    /// <returns>An awaitable Task with the parsed result of type <typeparamref name="T" /></returns>
    /// <exception cref="HttpRequestException">
    ///     Throws an exception if a non-success HTTP response was returned or if the result
    ///     couldn't be parsed.
    /// </exception>
    private async Task<T?> HttpRequest<T>(string? url = null, HttpMethod? verb = null, object? postData = null) where T : ApiResultBase
    {
        using HttpResponseMessage response = await HttpRequestRaw(url, verb, postData);
        string resultAsString = await response.Content.ReadAsStringAsync();
        T? res = JsonConvert.DeserializeObject<T>(resultAsString);

        try
        {
            if (res != null)
            {
                if (response.Headers.TryGetValues("Openai-Organization", out IEnumerable<string>? orgH)) res.Organization = orgH.FirstOrDefault();
                if (response.Headers.TryGetValues("X-Request-ID", out IEnumerable<string>? xreqId)) res.RequestId = xreqId.FirstOrDefault();

                if (response.Headers.TryGetValues("Openai-Processing-Ms", out IEnumerable<string>? pms))
                {
                    string? processing = pms.FirstOrDefault();
                    if (processing is not null && int.TryParse(processing, out int n)) res.ProcessingTime = TimeSpan.FromMilliseconds(n);
                }

                if (response.Headers.TryGetValues("Openai-Version", out IEnumerable<string>? oav)) res.RequestId = oav.FirstOrDefault();
                if (res.Model != null && string.IsNullOrEmpty(res.Model))
                {
                    if (response.Headers.TryGetValues("Openai-Model", out IEnumerable<string>? omd))
                    {
                        res.Model = omd.FirstOrDefault();
                    }
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Error parsing metadata: {e.Message}");
        }

        return res;
    }

    /// <summary>
    ///     Sends an HTTP Get request and does initial parsing
    /// </summary>
    /// <typeparam name="T">The <see cref="ApiResultBase" />-derived class for the result</typeparam>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request.  If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <returns>An awaitable Task with the parsed result of type <typeparamref name="T" /></returns>
    /// <exception cref="HttpRequestException">
    ///     Throws an exception if a non-success HTTP response was returned or if the result
    ///     couldn't be parsed.
    /// </exception>
    internal Task<T?> HttpGet<T>(string? url = null) where T : ApiResultBase
    {
        return HttpRequest<T>(url, HttpMethod.Get);
    }

    /// <summary>
    ///     Sends an HTTP Post request and does initial parsing
    /// </summary>
    /// <typeparam name="T">The <see cref="ApiResultBase" />-derived class for the result</typeparam>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request.  If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <param name="postData">(optional) A json-serializable object to include in the request body.</param>
    /// <returns>An awaitable Task with the parsed result of type <typeparamref name="T" /></returns>
    /// <exception cref="HttpRequestException">
    ///     Throws an exception if a non-success HTTP response was returned or if the result
    ///     couldn't be parsed.
    /// </exception>
    internal Task<T?> HttpPost<T>(string? url = null, object? postData = null) where T : ApiResultBase
    {
        return HttpRequest<T>(url, HttpMethod.Post, postData);
    }

    /// <summary>
    ///     Sends an HTTP Delete request and does initial parsing
    /// </summary>
    /// <typeparam name="T">The <see cref="ApiResultBase" />-derived class for the result</typeparam>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request.  If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <param name="postData">(optional) A json-serializable object to include in the request body.</param>
    /// <returns>An awaitable Task with the parsed result of type <typeparamref name="T" /></returns>
    /// <exception cref="HttpRequestException">
    ///     Throws an exception if a non-success HTTP response was returned or if the result
    ///     couldn't be parsed.
    /// </exception>
    internal Task<T?> HttpDelete<T>(string? url = null, object? postData = null) where T : ApiResultBase
    {
        return HttpRequest<T>(url, HttpMethod.Delete, postData);
    }

    /// <summary>
    ///     Sends an HTTP Put request and does initial parsing
    /// </summary>
    /// <typeparam name="T">The <see cref="ApiResultBase" />-derived class for the result</typeparam>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request.  If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <param name="postData">(optional) A json-serializable object to include in the request body.</param>
    /// <returns>An awaitable Task with the parsed result of type <typeparamref name="T" /></returns>
    /// <exception cref="HttpRequestException">
    ///     Throws an exception if a non-success HTTP response was returned or if the result
    ///     couldn't be parsed.
    /// </exception>
    internal Task<T?> HttpPut<T>(string? url = null, object? postData = null) where T : ApiResultBase
    {
        return HttpRequest<T>(url, HttpMethod.Put, postData);
    }

    /// <summary>
    ///     Sends an HTTP request and handles a streaming response.  Does basic line splitting and error handling.
    /// </summary>
    /// <param name="url">
    ///     (optional) If provided, overrides the url endpoint for this request. If omitted, then
    ///     <see cref="Url" /> will be used.
    /// </param>
    /// <param name="verb">
    ///     (optional) The HTTP verb to use, for example "<see cref="HttpMethod.Get" />".  If omitted, then
    ///     "GET" is assumed.
    /// </param>
    /// <param name="postData">(optional) A json-serializable object to include in the request body.</param>
    /// <param name="requestRef">(optional) A container for JSON-encoded outbound request.</param>
    /// <returns>The HttpResponseMessage of the response, which is confirmed to be successful.</returns>
    /// <exception cref="HttpRequestException">Throws an exception if a non-success HTTP response was returned</exception>
    protected async IAsyncEnumerable<T> HttpStreamingRequest<T>(string? url = null, HttpMethod? verb = null, object? postData = null, Ref<string>? requestRef = null) where T : ApiResultBase
    {
        using HttpResponseMessage response = await HttpRequestRaw(url, verb, postData, true);

        string? organization = null;
        string? requestId = null;
        TimeSpan processingTime = TimeSpan.Zero;
        string? openaiVersion = null;
        string? modelFromHeaders = null;

        try
        {
            if (response.Headers.TryGetValues("Openai-Organization", out IEnumerable<string>? orgH)) organization = orgH.FirstOrDefault();
            if (response.Headers.TryGetValues("X-Request-ID", out IEnumerable<string>? xreqId)) requestId = xreqId.FirstOrDefault();

            if (response.Headers.TryGetValues("Openai-Processing-Ms", out IEnumerable<string>? pms))
            {
                string? processing = pms.FirstOrDefault();
                if (processing is not null && int.TryParse(processing, out int n)) processingTime = TimeSpan.FromMilliseconds(n);
            }

            if (response.Headers.TryGetValues("Openai-Version", out IEnumerable<string>? oav)) openaiVersion = oav.FirstOrDefault();
            if (response.Headers.TryGetValues("Openai-Model", out IEnumerable<string>? omd)) modelFromHeaders = omd.FirstOrDefault();
        }
        catch (Exception e)
        {
            Debug.Print($"Issue parsing metadata of OpenAi Response.  Url: {url}, Error: {e}.  This is probably ignorable.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using StreamReader reader = new(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("data:")) line = line["data:".Length..];

            line = line.TrimStart();

            if (line == "[DONE]") yield break;

            if (line.StartsWith(':') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            
            T? res = JsonConvert.DeserializeObject<T>(line);

            if (res is null)
            {
                continue;
            }
            
            res.Organization = organization;
            res.RequestId = requestId;
            res.ProcessingTime = processingTime;
            res.OpenaiVersion = openaiVersion;

            if (res.Model != null && string.IsNullOrEmpty(res.Model))
            {
                if (modelFromHeaders != null)
                {
                    res.Model = modelFromHeaders;
                }
            }

            yield return res;
        }
    }
}
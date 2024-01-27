// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace OpenAiNg.Common;

public sealed class Error
{
    /// <summary>
    /// One of server_error or rate_limit_exceeded.
    /// </summary>
    [JsonInclude]
    [JsonProperty("code")]
    public string Code { get; private set; }

    /// <summary>
    /// A human-readable description of the error.
    /// </summary>
    [JsonInclude]
    [JsonProperty("message")]
    public string Message { get; private set; }
}

// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.Serialization;
namespace OpenAiNg.Common;

public enum SortOrder
{
    [EnumMember(Value = "desc")]
    Descending,
    [EnumMember(Value = "asc")]
    Ascending,
}

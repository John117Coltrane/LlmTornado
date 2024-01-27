﻿// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace OpenAiNg.FineTuning
{
    public sealed class HyperParameters
    {
        [JsonProperty("n_epochs")]
        public int? Epochs { get; set; }

        [JsonProperty("batch_size")]
        public int? BatchSize { get; set; }

        [JsonProperty("learning_rate_multiplier")]
        public int? LearningRateMultiplier { get; set; }
    }
}
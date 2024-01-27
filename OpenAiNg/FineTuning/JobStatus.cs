﻿// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace OpenAiNg.FineTuning
{
    public enum JobStatus
    {
        NotStarted = 0,
        ValidatingFiles,
        Queued,
        Running,
        Succeeded,
        Failed,
        Cancelled
    }
}
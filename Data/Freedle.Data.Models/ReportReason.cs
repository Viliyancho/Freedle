namespace Freedle.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public enum ReportReason
    {
        NotInterested = 1,
        Spam = 2,
        SensitiveContent = 3,
        AbusiveOrHarmful = 4,
        HateSpeech = 5,
        HarassmentOrBullying = 6,
        Other = 7,
    }
}

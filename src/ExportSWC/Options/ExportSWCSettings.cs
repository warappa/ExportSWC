using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace ExportSWC.Options
{
    [DataContract]
    [Serializable]
    public class ExportSWCSettings
    {
        [DataMember]
        [DisplayName("Overwrite default build command")]
        [Description("Overrides default build command and replaces it with ExportSWC's build")]
        public bool OverrideBuildCommand { get; set; } = true;
    }
}

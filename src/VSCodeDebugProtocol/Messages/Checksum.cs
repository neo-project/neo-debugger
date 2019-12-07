using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class Checksum : DebugType
    {
        [JsonProperty("algorithm")]
        private EnumValue<ChecksumAlgorithm> _algorithm = new EnumValue<ChecksumAlgorithm>();

        [JsonIgnore]
        public ChecksumAlgorithm Algorithm
        {
            get
            {
                return _algorithm.Value;
            }
            set
            {
                _algorithm.Value = value;
            }
        }

        [JsonProperty("checksum")]
        public string ChecksumValue
        {
            get;
            set;
        }

        public Checksum()
        {
        }

        public Checksum(ChecksumAlgorithm algorithm, string checksumValue)
        {
            Algorithm = algorithm;
            ChecksumValue = checksumValue;
        }
    }
}

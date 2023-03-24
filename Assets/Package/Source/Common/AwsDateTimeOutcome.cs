using System;

namespace Aws.GameLift
{
    public class AwsDateTimeOutcome : GenericOutcome
    {
        public DateTime Result { get; set; }

        public AwsDateTimeOutcome(DateTime result): base()
        {
            Result = result;
        }

        public AwsDateTimeOutcome(GameLiftError error) : base(error)
        {
        }

        public AwsDateTimeOutcome(GameLiftError error, DateTime result) : base(error)
        {
            Result = result;
        }
    }
}

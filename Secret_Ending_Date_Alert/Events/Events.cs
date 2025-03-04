using Kingmaker.Kingdom;
using static Secret_Ending_Date_Alert.Main;

namespace Secret_Ending_Date_Alert.Events
{
    public class OnDayChanged : IKingdomDayHandler
    {
        public void OnNewDay()
        {
            SecretEndingAlert();
        }
    }
}

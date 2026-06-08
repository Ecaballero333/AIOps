using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstrumentationInterface
{
    public interface ICustomMetrics
    {
            // Counters
            void LoginInvocations(long value = 1);

            /// <summary>
            /// Records a login attempt by outcome.
            /// </summary>
            void RecordLoginAttempt(string outcome);

            /// <summary>
            /// Records a business-domain event by event name and outcome.
            /// </summary>
            void RecordBusinessEvent(string eventName, string outcome);

            /// <summary>
            /// Records a created purchase, including amount and purchased item count.
            /// </summary>
            void RecordPurchaseCreated(decimal totalAmount, int itemCount);

            /// <summary>
            /// Records a status transition for a purchase detail.
            /// </summary>
            void RecordPurchaseStatusChange(string status);
            
            /// <summary>
            /// Records an HTTP request with endpoint, method, and status code
            /// </summary>
            void RecordHttpRequest(string endpoint, string method, int statusCode);
            
            /// <summary>
            /// Records an error/exception for a specific endpoint
            /// </summary>
            void RecordError(string endpoint, string errorType);

            // Gauges
            void SetActiveUserCount(int count);

            // Histograms / Timings
            void RequestDuration(double milliseconds);
            
            /// <summary>
            /// Records the duration of a specific endpoint request
            /// </summary>
            void RecordEndpointDuration(string endpoint, string method, double milliseconds);
        
    }
}

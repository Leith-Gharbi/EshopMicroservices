
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BuildingBlocks.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse> 
        (ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse> 
        where TRequest : notnull , IRequest<TResponse> 
        where TResponse : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            logger.LogInformation("[START] Handle Request={Request} - Response={Response} - RequestData={RequestData}",
                typeof(TRequest).Name, typeof(TResponse).Name, request);  
            
            var timer= Stopwatch.StartNew();
            timer.Start();  

            var response = await next(); // Call the next delegate/middleware in the pipeline

            timer.Stop();   
            var timeTaken= timer.Elapsed;
            if(timeTaken.Seconds>3) // log warning if time taken is more than 3 seconds
            {
                logger.LogWarning("[PERFORMANCE] The Request={Request} took={TimeTaken}",
                    typeof(TRequest).Name, timeTaken.Seconds);  
            }
            else
            {
                logger.LogInformation("[END] Handle Request={Request} - Response={Response} - TimeTaken={TimeTaken}",
                    typeof(TRequest).Name, typeof(TResponse).Name, timeTaken);
            }
            return response;    
        }
    }
}

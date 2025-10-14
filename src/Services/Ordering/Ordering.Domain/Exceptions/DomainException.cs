

namespace Ordering.Domain.Exceptions;

    public class DomainException :Exception
    {
        public string Details { get; set; } = default!;

        public DomainException(string message) : base($"Doamin Exception : \"{message}\" throws from domain layer." ) { }

    }
   

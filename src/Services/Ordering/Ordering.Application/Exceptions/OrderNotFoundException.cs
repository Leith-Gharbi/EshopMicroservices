
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Xml.Linq;

namespace Ordering.Application.Exceptions
{
    public class OrderNotFoundException :  NotFoundException
    {
        public OrderNotFoundException(Guid id) : base("Order", id)
        {
        }


    }
}

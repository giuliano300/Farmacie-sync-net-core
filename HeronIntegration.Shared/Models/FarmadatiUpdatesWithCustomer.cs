using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class FarmadatiUpdatesWithCustomer
    {
        public FarmadatiUpdates FarmadatiUpdate { get; set; }
        public Customer Customer { get; set; }

    }
}

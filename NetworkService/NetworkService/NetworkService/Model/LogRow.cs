using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkService.Model
{
    public class LogRow
    {
        public int Id { get; set; }
        public double CurrentValue { get; set; }
        public DateTime Date { get; set; }
    }
}

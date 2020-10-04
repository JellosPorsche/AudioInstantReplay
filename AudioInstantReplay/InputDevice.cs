using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioInstantReplay
{
    public class InputDevice
    {
        public string Name { get; set; }
        public int DeviceId { get; set; }

        // Override ToString for ComboBox
        public override string ToString()
        {
            return Name;
        }
    }
}

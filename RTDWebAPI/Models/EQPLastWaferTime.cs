using System;

namespace RTDWebAPI.Models
{
    public class EQPLastWaferTime
    {
        public string EquipID { get; set; }
        public string PortID { get; set; }
        public string RecipeID { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
    }
}

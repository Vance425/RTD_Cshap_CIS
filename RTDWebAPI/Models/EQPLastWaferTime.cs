using System;

namespace RTDWebAPI.Models
{
    public class EQPLastWaferTime
    {
        public string EquipID { get; set; }
        public string PortID { get; set; }
        public string RecipeID { get; set; }
        public string LotID { get; set; }
        public float Hours { get; set; }
        public float Minutes { get; set; }
        public float Seconds { get; set; }
    }
}

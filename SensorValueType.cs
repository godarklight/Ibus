using System;

namespace Ibus
{
    public class SensorValueType : Attribute
    {
        public Type type;
        public SensorValueType(Type type)
        {
            this.type = type;
        }
    }
}
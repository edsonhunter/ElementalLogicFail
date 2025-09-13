using System;
using ElementLogicFail.Scripts.Domain.Interface;

namespace ElementLogicFail.Scripts.Domain
{
    public class Element : IElement
    {
        public ElementType Type { get; }

        private Element()
        {
            Type = ElementType.Unknown;
        }
        
        public Element(ElementType type) : this()
        {
            Type = type;
        }
        
        public bool IsSameType(IElement element)
        {
            if (element == null)
            {
                throw new InvalidOperationException($"element object is null");
            }
            return element.Type == Type;
        }
    }
}
using System;
using ElementLogicFail.Scripts.Domain;
using ElementLogicFail.Scripts.Domain.Interface;
using NUnit.Framework;
using Random = UnityEngine.Random;

namespace ElementLogicFail.Scripts.Tests
{
    public class ElementsTests
    {
        private IElement CreateElement(ElementType type)
        {
            return new Element(type);
        }

        private IElement CreateRandomElement()
        {
            var rand = Random.Range(0, Enum.GetValues(typeof(ElementType)).Length);
            return new Element((ElementType)rand);
        }

        [Test]
        public void CheckTypes()
        {
            var water1 = CreateElement(ElementType.Water);
            Assert.That(water1.Type, Is.EqualTo(ElementType.Water));
            var water2 = CreateElement(ElementType.Water);
            Assert.True(water1.IsSameType(water2));
            var fire1 = CreateElement(ElementType.Fire);
            Assert.False(fire1.IsSameType(water1));
        }
        
        [Test]
        public void CheckRandomTypes()
        {
            var element1 = CreateRandomElement();
            var element2 = CreateRandomElement();
            var isSameType = element1.IsSameType(element2);
            if (isSameType)
            {
                Assert.True(element1.IsSameType(element2));
            }
            else
            {
                Assert.False(element1.IsSameType(element2));
            }
        }
    }
}
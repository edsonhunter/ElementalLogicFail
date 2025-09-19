namespace ElementLogicFail.Scripts.Domain.Interface
{
    public interface IElement
    {
        ElementType Type { get; }
        
        bool IsSameType(IElement element);
    }

    public enum ElementType
    {
        Unknown,
        Fire,
        Water,
        Earth,
        Wind
    }
    
    //Future ideas. Joining different elements together results in different outcomes:
    //Fire+Water=Smoke
    //Fire+Earth=Lava
    //Fire+Wind=Explosion
    //Water+Earth=Mud
    //Water+Wind=Rain
    //Earth+Wind=Hurricane
}
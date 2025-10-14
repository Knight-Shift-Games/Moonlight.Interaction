namespace Moonlight.Interaction
{
    public struct InteractionOptionDto
    {
        public InteractionOption Option;
        public int Index;

        public InteractionOptionDto(InteractionOption option, int index)
        {
            Option = option;
            Index = index;
        }
    }
}
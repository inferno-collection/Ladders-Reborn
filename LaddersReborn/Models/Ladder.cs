namespace InfernoCollection.LaddersReborn.Models
{
    public class Ladder
    {
        public int NetworkId { get; set; } = -1;
        public float[] Position { get; set; }
        public Status Status { get; set; }
    }

    public enum ClimbingDirection
    {
        Up,
        Down
    }

    public enum Status
    {
        NotCreated,
        BeingCarried,
        BeingClimbed,
        Placed,
        Dropped
    }
}
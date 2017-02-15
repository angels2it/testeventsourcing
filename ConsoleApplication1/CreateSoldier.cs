using System;
using TinyMessenger;

namespace ConsoleApplication1
{
    public class CreateSoldierEvent : DomainEvent
    {
        public CreateSoldierEvent(Guid id, TimeSpan productionTime)
        {
            Id = id;
            RunningAt = DateTime.UtcNow;
            ProductionTime = productionTime;
        }
        public Guid Id { get; set; }
        public TimeSpan ProductionTime { get; set; }
    }

    public class CreateGameEvent : DomainEvent
    {

    }

    public class CreateCastleEvent : DomainEvent
    {

    }

    public interface IDomainEvent
    {
        DateTime RunningAt { get; set; }
    }
    public abstract class DomainEvent : IDomainEvent
    {
        public DateTime RunningAt { get; set; }
    }

    public class SoldierCreatedEvent : TinyMessageBase
    {
        public SoldierCreatedEvent(object sender) : base(sender)
        {
        }

        public DateTime EndedAt { get; set; }
    }
}
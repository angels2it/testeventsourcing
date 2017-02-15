using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using NEventStore;

namespace ConsoleApplication1
{
    public interface IHandlerRegistrar
    {
        void RegisterHandler<T>(Func<DomainEventHandlerData<T>, bool> handler) where T : IDomainEvent;
    }

    public class HandlerRegistrar : IHandlerRegistrar
    {
        public void RegisterHandler<T>(Func<DomainEventHandlerData<T>, bool> handler) where T : IDomainEvent
        {
            EventHandler.Add<T>(handler);
        }
    }

    public static class EventHandler
    {
        private static readonly Dictionary<Type, Func<DomainEventHandlerData<IDomainEvent>, bool>> Routes =
            new Dictionary<Type, Func<DomainEventHandlerData<IDomainEvent>, bool>>();

        private static IContainer _container;
        public static void Init(IContainer container)
        {
            _container = container;
            var executorTypes = Assembly.GetAssembly(typeof(IDomainEventHandler<>)).ExportedTypes
                .Select(t => new { Type = t, Interfaces = ResolveMessageHandlerInterface(t) })
                .Where(e => e.Interfaces != null && e.Interfaces.Any());

            foreach (var executorType in executorTypes)
                foreach (var @interface in executorType.Interfaces)
                    InvokeHandler(@interface, new HandlerRegistrar(), executorType.Type);
        }

        private static void InvokeHandler(Type @interface, IHandlerRegistrar bus, Type executorType)
        {
            var commandType = @interface.GenericTypeArguments[0];
            var registerExecutorMethod = bus
                .GetType().GetRuntimeMethods()
                .Where(mi => mi.Name == "RegisterHandler")
                .Where(mi => mi.IsGenericMethod)
                .Where(mi => mi.GetGenericArguments().Count() == 1)
                .Single(mi => mi.GetParameters().Count() == 1)
                .MakeGenericMethod(commandType);

            var del = new Func<dynamic, bool>(x =>
            {
                dynamic handler = _container.Resolve(@interface);
                var d1 = typeof(DomainEventHandlerData<>);
                Type[] typeArgs = { x.Event.GetType() };
                var makeme = d1.MakeGenericType(typeArgs);
                var a = DomainEventHandlerData<DomainEvent>.CreateDynamicInstance(makeme, x);
                return handler.Handle(a);
            });

            registerExecutorMethod.Invoke(bus, new object[] { del });
        }

        private static IEnumerable<Type> ResolveMessageHandlerInterface(Type type)
        {
            return type.GetTypeInfo().ImplementedInterfaces
                .Where(
                    i =>
                        i.IsConstructedGenericType && ((i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))));
        }

        public static void Add<T>(Func<DomainEventHandlerData<T>, bool> handler) where T : IDomainEvent
        {
            Func<DomainEventHandlerData<IDomainEvent>, bool> handlers;
            if (!Routes.TryGetValue(typeof(T), out handlers))
            {
                Routes.Add(typeof(T), (Func<DomainEventHandlerData<IDomainEvent>, bool>)handler);
            }
        }

        public static bool Publish<T>(DomainEventHandlerData<T> @event) where T : DomainEvent
        {
            Func<DomainEventHandlerData<IDomainEvent>, bool> handlers;
            if (!Routes.TryGetValue(@event.Event.GetType(), out handlers)) return false;
            return (bool)handlers.DynamicInvoke(@event);
        }
    }

    public class CreateSoldierEventHandler : IDomainEventHandler<CreateSoldierEvent>
    {
        public bool Handle(DomainEventHandlerData<CreateSoldierEvent> data)
        {
            if (data.EventObject.RunningAt.Add(data.EventObject.ProductionTime).CompareTo(DateTime.UtcNow) >= 1)
            {
                var remainTime = data.EventObject.RunningAt.Add(data.EventObject.ProductionTime).Subtract(DateTime.UtcNow);
                Console.WriteLine($"Need {remainTime.TotalSeconds} seconds to production new soldier...");
                return false;
            }
            var gameSnapshot = data.Snapshot as CastleAggregate;
            if (gameSnapshot != null)
            {
                Console.WriteLine("Soldier created!");
                gameSnapshot.SoldiersAmount++;
                gameSnapshot.Soldiers.Add(data.EventObject.Id);
            }
            Console.WriteLine("Create more soldier...");
            Program.MessageHub.Publish(new SoldierCreatedEvent(this)
            {
                EndedAt = data.EventObject.RunningAt.Add(data.EventObject.ProductionTime)
            });
            return true;
        }
    }

    public interface IDomainEventHandler<T> where T : IDomainEvent
    {
        bool Handle(DomainEventHandlerData<T> @event);
    }
    public class DomainEventHandlerData<T> where T : IDomainEvent
    {
        public object Event { get; set; }
        public T EventObject => (T) Event;
        public Aggregate Snapshot { get; set; }
        public IStoreEvents Store { get; set; }
        public Guid Id { get; set; }

        public static dynamic CreateDynamicInstance(Type type, DomainEventHandlerData<DomainEvent> data)
        {
            dynamic a = Activator.CreateInstance(type);
            a.Event = data.Event;
            a.Snapshot = data.Snapshot;
            a.Store = data.Store;
            a.Id = data.Id;
            return a;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;

namespace ConsoleApplication1
{
    public class CastleAggregate : Aggregate
    {
        public int SoldiersAmount { get; set; }
        public List<Guid> Soldiers { get; set; }
        public Guid Id { get; set; }
    }

    public class SoldierAggregate : Aggregate
    {
    }
    public class Aggregate
    {
    }
}
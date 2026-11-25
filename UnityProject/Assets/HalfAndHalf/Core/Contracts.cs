using System;
using System.Collections.Generic;

namespace HalfAndHalf.Core
{
    public enum Grade { None, C, B, A, S, SPlus, Perfect }
    public enum AttemptPhase { Dropping, Ready, Cutting, Result, Invalid }
    public enum InvalidReason { None, TimedOut, Interrupted }

    public readonly struct MassCell
    {
        public readonly decimal MinX, MaxX, MinY, MaxY, Mass;
        public MassCell(decimal minX, decimal maxX, decimal minY, decimal maxY, decimal mass)
        { MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY; Mass = mass; }
    }

    public sealed class MassMap
    {
        public IReadOnlyList<MassCell> Cells { get; }
        public decimal TotalMass { get; }
        readonly Column[] columns;
        readonly MassCell[] unorderedCells;
        readonly FractionRange[] fractionRanges;
        readonly int[] fractionIds;
        readonly struct FractionRange:IEquatable<FractionRange>
        {
            internal readonly decimal Min,Max;
            readonly int minFlags,maxFlags;
            internal FractionRange(MassCell cell){Min=cell.MinX;Max=cell.MaxX;minFlags=decimal.GetBits(Min)[3];maxFlags=decimal.GetBits(Max)[3];}
            public bool Equals(FractionRange other)=>Min==other.Min&&Max==other.Max&&minFlags==other.minFlags&&maxFlags==other.maxFlags;
            public override bool Equals(object other)=>other is FractionRange range&&Equals(range);
            public override int GetHashCode(){unchecked{return ((Min.GetHashCode()*397^Max.GetHashCode())*397^minFlags)*397^maxFlags;}}
        }
        struct Column { public decimal Min,Max,Prefix;public int Start,End;public bool ShareFraction; }
        static Column[] OrderedColumns(List<MassCell> cells)
        {
            var result=new List<Column>();decimal prefix=0;
            for(int i=0;i<cells.Count;)
            {
                var first=cells[i];if(result.Count>0&&first.MinX<result[result.Count-1].Max)return null;
                var column=new Column{Min=first.MinX,Max=first.MaxX,Prefix=prefix,Start=i,ShareFraction=true};
                int minFlags=decimal.GetBits(first.MinX)[3],maxFlags=decimal.GetBits(first.MaxX)[3];
                do
                {
                    // Equal values with equal flags have identical decimal bits; mixed scales keep the original path.
                    if(column.ShareFraction&&(decimal.GetBits(cells[i].MinX)[3]!=minFlags||decimal.GetBits(cells[i].MaxX)[3]!=maxFlags))column.ShareFraction=false;
                    prefix+=cells[i].Mass;i++;
                }while(i<cells.Count&&cells[i].MinX==column.Min&&cells[i].MaxX==column.Max);
                column.End=i;result.Add(column);
            }
            return result.ToArray();
        }
        public MassMap(IEnumerable<MassCell> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            var copy = new List<MassCell>(cells);
            foreach (var cell in copy)
            {
                if (cell.MaxX <= cell.MinX || cell.MaxY <= cell.MinY || cell.Mass < 0)
                    throw new ArgumentException("Mass cells require positive area and non-negative mass.");
                TotalMass += cell.Mass;
            }
            if (TotalMass <= 0) throw new ArgumentException("A mass map needs positive total mass.");
            Cells = copy.AsReadOnly();
            columns = OrderedColumns(copy);
            if(columns==null)
            {
                unorderedCells=copy.ToArray();fractionIds=new int[copy.Count];
                var lookup=new Dictionary<FractionRange,int>();var ranges=new List<FractionRange>();
                for(int i=0;i<copy.Count;i++)
                {
                    var range=new FractionRange(copy[i]);
                    if(!lookup.TryGetValue(range,out int index)){index=ranges.Count;ranges.Add(range);lookup.Add(range,index);}
                    fractionIds[i]=index;
                }
                fractionRanges=ranges.ToArray();
            }
        }
        public MassSplit Split(decimal localKnifeX)
        {
            decimal left = 0;
            if(columns!=null)
            {
                int lo=0,hi=columns.Length;
                while(lo<hi){int mid=lo+(hi-lo)/2;if(columns[mid].Max<=localKnifeX)lo=mid+1;else hi=mid;}
                if(lo==columns.Length)return new MassSplit(TotalMass,0);
                var column=columns[lo];left=column.Prefix;
                decimal shared=localKnifeX>column.Min&&column.ShareFraction?Math.Max(0m,Math.Min(1m,(localKnifeX-column.Min)/(column.Max-column.Min))):0m;
                if(localKnifeX>column.Min)
                for(int i=column.Start;i<column.End;i++)
                {
                    var cell=Cells[i];
                    // Preserve each rounded product and its addition order within this column.
                    decimal fraction=column.ShareFraction?shared:Math.Max(0m,Math.Min(1m,(localKnifeX-cell.MinX)/(cell.MaxX-cell.MinX)));
                    left+=cell.Mass*fraction;
                }
                return new MassSplit(left,TotalMass-left);
            }
            // Share identical fractions and their classification; preserve each cell's arithmetic and order.
            Span<decimal> fractions=fractionRanges.Length<=128?stackalloc decimal[fractionRanges.Length]:new decimal[fractionRanges.Length];
            Span<byte> fractionKinds=fractionRanges.Length<=128?stackalloc byte[fractionRanges.Length]:new byte[fractionRanges.Length];
            for(int i=0;i<fractionRanges.Length;i++)
            {
                var range=fractionRanges[i];
                fractions[i]=localKnifeX<=range.Min?0m:localKnifeX>=range.Max?1m:Math.Max(0m,Math.Min(1m,(localKnifeX-range.Min)/(range.Max-range.Min)));
                fractionKinds[i]=fractions[i]==0? (byte)1:fractions[i]==1? (byte)2:(byte)0;
            }
            for(int i=0;i<unorderedCells.Length;i++)
            {
                var cell=unorderedCells[i];int fractionId=fractionIds[i];byte kind=fractionKinds[fractionId];
                if(kind==1)continue;
                if(kind==2){left+=cell.Mass;continue;}
                left+=cell.Mass*fractions[fractionId];
            }
            return new MassSplit(left, TotalMass - left);
        }
    }

    public readonly struct MassSplit
    {
        public readonly decimal Left, Right;
        public MassSplit(decimal left, decimal right) { Left = left; Right = right; }
    }

    // Screen-plane state. Cells are scaled, rotated, then translated before vertical clipping.
    public readonly struct TransformState
    {
        public readonly decimal X, Y, ScaleX, ScaleY, RotationDegrees;
        public TransformState(decimal x, decimal y, decimal scaleX, decimal scaleY, decimal rotationDegrees = 0)
        { X = x; Y = y; ScaleX = scaleX; ScaleY = scaleY; RotationDegrees = rotationDegrees; }
        public static TransformState Identity => new TransformState(0, 0, 1, 1);
    }

    public sealed class ObjectDefinition
    {
        public string Id { get; }
        public string ContentVersion { get; }
        public MassMap Mass { get; }
        public string PresentationReference { get; }
        readonly Func<long, MassMap> dynamicMass;
        public MassMap MassAt(long elapsedMilliseconds) => dynamicMass == null ? Mass : dynamicMass(Math.Max(0, elapsedMilliseconds));
        public ObjectDefinition(string id, string contentVersion, MassMap mass, string presentationReference)
        { Id = id; ContentVersion = contentVersion; Mass = mass; PresentationReference = presentationReference; }
        public ObjectDefinition(string id, string contentVersion, MassMap mass, string presentationReference, Func<long, MassMap> dynamicMass)
            : this(id, contentVersion, mass, presentationReference)
        { this.dynamicMass = dynamicMass ?? throw new ArgumentNullException(nameof(dynamicMass)); }
    }

    public sealed class CutSnapshot
    {
        public string ObjectId { get; }
        public string ContentVersion { get; }
        public decimal KnifeX { get; }
        public long ElapsedMilliseconds { get; }
        public TransformState Transform { get; }
        public bool OnlineValid { get; }
        public CutSnapshot(string objectId, string contentVersion, decimal knifeX, long elapsedMilliseconds, TransformState transform, bool onlineValid)
        { ObjectId = objectId; ContentVersion = contentVersion; KnifeX = knifeX; ElapsedMilliseconds = elapsedMilliseconds; Transform = transform; OnlineValid = onlineValid; }
    }

    public sealed class AttemptResult
    {
        public CutSnapshot Snapshot { get; }
        public decimal LeftMass { get; }
        public decimal RightMass { get; }
        public int AccuracyUnits { get; }
        public Grade Grade => Scoring.GradeFor(AccuracyUnits);
        public long ElapsedMilliseconds => Snapshot.ElapsedMilliseconds;
        public bool OnlineValid => Snapshot.OnlineValid;
        public decimal LeftPercent => 100m * LeftMass / (LeftMass + RightMass);
        public decimal RightPercent => 100m * RightMass / (LeftMass + RightMass);
        public AttemptResult(CutSnapshot snapshot, decimal leftMass, decimal rightMass, int accuracyUnits)
        { Snapshot = snapshot; LeftMass = leftMass; RightMass = rightMass; AccuracyUnits = accuracyUnits; }
    }
}

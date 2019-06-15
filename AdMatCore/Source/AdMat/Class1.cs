using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;


namespace AdMat
{
    public static class ThingDefOf
    {
        public static ThingDef Graphene;

        public static ThingDef GrapheneCartridge;

        public static ThingDef GrapheneVat;
    }

    public static class JobDefOf
    {
        public static JobDef FillGrapheneVat;

        public static JobDef TakeGrapheneOutOfGrapheneVat;
    }

    [StaticConstructorOnStartup]
    public class Building_GrapheneVat : Building
    {
        private int GrapheneCartridgeCount;

        private float progressInt;

        private Material barFilledCachedMat;

        public const int MaxGrapheneCartridgeCapacity = 25;

        private const int BaseReactionDuration = 360000;

        public const float MinIdealTemperature = 7f;

        private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);

        private static readonly Color BarZeroProgressColor = new Color(0.4f, 0.27f, 0.22f);

        private static readonly Color BarFermentedColor = new Color(0.6f, 0.93f, 0.96f);

        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));

        public float Progress
        {
            get
            {
                return progressInt;
            }
            set
            {
                if (value != progressInt)
                {
                    progressInt = value;
                    barFilledCachedMat = null;
                }
            }
        }

        private Material BarFilledMat
        {
            get
            {
                if (barFilledCachedMat == null)
                {
                    barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(BarZeroProgressColor, BarFermentedColor, Progress));
                }
                return barFilledCachedMat;
            }
        }

        public int SpaceLeftForGrapheneCartridge
        {
            get
            {
                if (Reacted)
                {
                    return 0;
                }
                return 25 - GrapheneCartridgeCount;
            }
        }

        private bool Empty => GrapheneCartridgeCount <= 0;

        public bool Reacted => !Empty && Progress >= 1f;

        private float CurrentTempProgressSpeedFactor
        {
            get
            {
                CompProperties_TemperatureRuinable compProperties = def.GetCompProperties<CompProperties_TemperatureRuinable>();
                float ambientTemperature = base.AmbientTemperature;
                if (ambientTemperature < compProperties.minSafeTemperature)
                {
                    return 0.1f;
                }
                if (ambientTemperature < 7f)
                {
                    return GenMath.LerpDouble(compProperties.minSafeTemperature, 7f, 0.1f, 1f, ambientTemperature);
                }
                return 1f;
            }
        }

        private float ProgressPerTickAtCurrentTemp => 2.77777781E-06f * CurrentTempProgressSpeedFactor;

        private int EstimatedTicksLeft => Mathf.Max(Mathf.RoundToInt((1f - Progress) / ProgressPerTickAtCurrentTemp), 0);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref GrapheneCartridgeCount, "GrapheneCartridgeCount", 0);
            Scribe_Values.Look(ref progressInt, "progress", 0f);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Empty)
            {
                Progress = Mathf.Min(Progress + 250f * ProgressPerTickAtCurrentTemp, 1f);
            }
        }

        public void AddGrapheneCartridge(int count)
        {
            GetComp<CompTemperatureRuinable>().Reset();
            if (Reacted)
            {
                Log.Warning("Tried to add a graphene cartridge to a depositor filled with graphene sheets. Colonists should remove the graphene sheets first.");
                return;
            }
            int num = Mathf.Min(count, 25 - GrapheneCartridgeCount);
            if (num > 0)
            {
                Progress = GenMath.WeightedAverage(0f, num, Progress, GrapheneCartridgeCount);
                GrapheneCartridgeCount += num;
            }
        }

        protected override void ReceiveCompSignal(string signal)
        {
            if (signal == "RuinedByTemperature")
            {
                Reset();
            }
        }

        private void Reset()
        {
            GrapheneCartridgeCount = 0;
            Progress = 0f;
        }

        public void AddGrapheneCartridge(Thing GrapheneCartridge)
        {
            int num = Mathf.Min(GrapheneCartridge.stackCount, 25 - GrapheneCartridgeCount);
            if (num > 0)
            {
                AddGrapheneCartridge(num);
                GrapheneCartridge.SplitOff(num).Destroy();
            }
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (stringBuilder.Length != 0)
            {
                stringBuilder.AppendLine();
            }
            CompTemperatureRuinable comp = GetComp<CompTemperatureRuinable>();
            if (!Empty && !comp.Ruined)
            {
                if (Reacted)
                {
                    stringBuilder.AppendLine("ContainsGraphene".Translate(GrapheneCartridgeCount, 25));
                }
                else
                {
                    stringBuilder.AppendLine("ContainsGrapheneCartridge".Translate(GrapheneCartridgeCount, 25));
                }
            }
            if (!Empty)
            {
                if (Reacted)
                {
                    stringBuilder.AppendLine("Complete".Translate());
                }
                else
                {
                    stringBuilder.AppendLine("ReactionProgress".Translate(Progress.ToStringPercent(), EstimatedTicksLeft.ToStringTicksToPeriod()));
                    if (CurrentTempProgressSpeedFactor != 1f)
                    {
                        stringBuilder.AppendLine("GrapheneVatOutOfIdealTemperature".Translate(CurrentTempProgressSpeedFactor.ToStringPercent()));
                    }
                }
            }

            stringBuilder.AppendLine("Temperature".Translate() + ": " + base.AmbientTemperature.ToStringTemperature("F0"));
            stringBuilder.AppendLine("IdealReactingTemperature".Translate() + ": " + 7f.ToStringTemperature("F0") + " ~ " + comp.Props.maxSafeTemperature.ToStringTemperature("F0"));
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public Thing TakeOutGraphene()
        {
            if (!Reacted)
            {
                Log.Warning("Tried to get graphene sheets, but it's not yet complete.");
                return null;
            }
            Thing thing = ThingMaker.MakeThing(ThingDefOf.Graphene);
            thing.stackCount = GrapheneCartridgeCount;
            Reset();
            return thing;
        }

        public override void Draw()
        {
            base.Draw();
            if (!Empty)
            {
                Vector3 drawPos = DrawPos;
                drawPos.y += 3f / 64f;
                drawPos.z += 0.25f;
                GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
                r.center = drawPos;
                r.size = BarSize;
                r.fillPercent = (float)GrapheneCartridgeCount / 25f;
                r.filledMat = BarFilledMat;
                r.unfilledMat = BarUnfilledMat;
                r.margin = 0.1f;
                r.rotation = Rot4.North;
                GenDraw.DrawFillableBar(r);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (Prefs.DevMode && !Empty)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Debug: Set progress to 1",
                    action = delegate
                    {
                        Progress = 1f;
                    }
                };
            }
        }
    }
    public class WorkGiver_LoadGrapheneVat : WorkGiver_Scanner
    {
        private static string TemperatureTrans;

        private static string NoGrapheneCartridgeTrans;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDefOf.GrapheneVat);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public static void ResetStaticData()
        {
            TemperatureTrans = "BadTemperature".Translate().ToLower();
            NoGrapheneCartridgeTrans = "NoGrapheneCartridge".Translate();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_GrapheneVat building_GrapheneVat = t as Building_GrapheneVat;
            if (building_GrapheneVat == null || building_GrapheneVat.Reacted || building_GrapheneVat.SpaceLeftForGrapheneCartridge <= 0)
            {
                return false;
            }
            float ambientTemperature = building_GrapheneVat.AmbientTemperature;
            CompProperties_TemperatureRuinable compProperties = building_GrapheneVat.def.GetCompProperties<CompProperties_TemperatureRuinable>();
            if (ambientTemperature < compProperties.minSafeTemperature + 2f || ambientTemperature > compProperties.maxSafeTemperature - 2f)
            {
                JobFailReason.Is(TemperatureTrans);
                return false;
            }
            if (!t.IsForbidden(pawn))
            {
                LocalTargetInfo target = t;
                bool ignoreOtherReservations = forced;
                if (pawn.CanReserve(target, 1, -1, null, ignoreOtherReservations))
                {
                    if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
                    {
                        return false;
                    }
                    Thing thing = FindGrapheneCartridge(pawn, building_GrapheneVat);
                    if (thing == null)
                    {
                        JobFailReason.Is(NoGrapheneCartridgeTrans);
                        return false;
                    }
                    if (t.IsBurning())
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_GrapheneVat vat = (Building_GrapheneVat)t;
            Thing t2 = FindGrapheneCartridge(pawn, vat);
            return new Job(JobDefOf.FillGrapheneVat, t, t2);
        }

        private Thing FindGrapheneCartridge(Pawn pawn, Building_GrapheneVat barrel)
        {
            Predicate<Thing> predicate = delegate (Thing x)
            {
                if (x.IsForbidden(pawn) || !pawn.CanReserve(x))
                {
                    return false;
                }
                return true;
            };
            IntVec3 position = pawn.Position;
            Map map = pawn.Map;
            ThingRequest thingReq = ThingRequest.ForDef(ThingDefOf.GrapheneCartridge);
            PathEndMode peMode = PathEndMode.ClosestTouch;
            TraverseParms traverseParams = TraverseParms.For(pawn);
            Predicate<Thing> validator = predicate;
            return GenClosest.ClosestThingReachable(position, map, thingReq, peMode, traverseParams, 9999f, validator);
        }
    }

    public class WorkGiver_TakeGrapheneOutOfGrapheneVat : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDefOf.GrapheneVat);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_GrapheneVat building_GrapheneVat = t as Building_GrapheneVat;
            if (building_GrapheneVat == null || !building_GrapheneVat.Reacted)
            {
                return false;
            }
            if (t.IsBurning())
            {
                return false;
            }
            if (!t.IsForbidden(pawn))
            {
                LocalTargetInfo target = t;
                bool ignoreOtherReservations = forced;
                if (pawn.CanReserve(target, 1, -1, null, ignoreOtherReservations))
                {
                    return true;
                }
            }
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return new Job(JobDefOf.TakeGrapheneOutOfGrapheneVat, t);
        }
    }

}
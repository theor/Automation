using Unity.Entities;

namespace Automation
{

    [UpdateAfter(typeof(BeltUpdateSystem))]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    class BeltSplitterUpdateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var segments = GetComponentDataFromEntity<BeltSegment>();
            var items = GetBufferFromEntity<BeltItem>();
            Entities.ForEach((Entity e, ref BeltSplitter s) =>
                {
                    if (s.Input.Type != EntityType.None)
                    {
                        BeltItem i = s.UseOutput2 ? s.Output2 : s.Output1;
                        if (i.Type == EntityType.None)
                        {
                            // move it straight to output
                            if (s.UseOutput2)
                                s.Output2 = s.Input;
                            else
                                s.Output1 = s.Input;
                            s.Input.Type = EntityType.None;
                            s.UseOutput2 = !s.UseOutput2;
                        }
                    }

                    ProcessOutput(ref segments, ref items, ref s.Output1, s.Next1);
                    ProcessOutput(ref segments, ref items, ref s.Output2, s.Next2);
                    // s.
                })
                .WithNativeDisableContainerSafetyRestriction(segments)
                .WithNativeDisableContainerSafetyRestriction(items)
                .ScheduleParallel();
        }

        private static void ProcessOutput(ref ComponentDataFromEntity<BeltSegment> segments,
            ref BufferFromEntity<BeltItem> items, ref BeltItem sOutput1, Entity enext)
        {
            if (sOutput1.Type != EntityType.None)
            {
                var next = segments[enext];
                if (next.DistanceToInsertAtStart == 0) // full
                    return;
                if (sOutput1.Distance > 0)
                {
                    sOutput1.Distance--;
                    return;
                }

                if (BeltUpdateSystem.InsertInSegment(ref items, ref segments, sOutput1, enext))
                    sOutput1.Type = EntityType.None;
            }
        }
    }
}
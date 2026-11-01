using System;
using System.Collections.Generic;
using FactorySabotage.Gameplay.Traffic;
using UnityEngine;

internal static class TrafficChecks
{
    private static int _checks;
    private static readonly Vector3 HalfSize = new Vector3(0.1f, 0.08f, 0.22f);

    private static void Expect(bool condition, string description)
    {
        if (!condition) throw new Exception("FAILED: " + description);
        _checks++;
        Console.WriteLine("PASS: " + description);
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;
    private static TrafficPose Pose(float x, float z, float yaw = 0f, float height = 0f) =>
        new TrafficPose(new Vector3(x, height, z), yaw);
    private static TrafficFootprint Box(TrafficPose pose, float gap = 0.03f) =>
        TrafficFootprint.At(pose, Vector3.zero, HalfSize, gap);
    private static TrafficFootprint Sweep(TrafficPose from, TrafficPose to) =>
        TrafficFootprint.Swept(from, to, Vector3.zero, HalfSize, 0.03f);

    private sealed class Car
    {
        public int Id;
        public float Distance;
        public float Speed;
        public TrafficPose Pose;
    }

    private static void Main()
    {
        var path = new TrafficPath(new[] { Vector3.zero, new Vector3(0, 0, 2), new Vector3(2, 0, 2) }, false);
        Expect(path.IsValid && Near(path.Length, 4f) && Near(path.Position(1f).z, 1f),
            "Routes advance by world distance along their lane segments");
        Expect(Near(path.Position(100f).x, 2f) && Near(path.Position(100f).z, 2f),
            "An open route stops at its endpoint");
        Expect(Near(path.DistanceToCorner(2f), 2f) && path.Direction(2f).x > 0.99f,
            "Reaching a waypoint advances to the next segment without a stuck zero-length step");
        var loop = new TrafficPath(new[] { Vector3.zero, new Vector3(0, 0, 3), new Vector3(3, 0, 3), new Vector3(3, 0, 0) }, true);
        Expect(Near(loop.Length, 12f) && loop.Position(loop.Length).sqrMagnitude < 0.0001f,
            "A loop includes the closing segment and wraps without teleporting");
        Expect(!new TrafficPath(new[] { Vector3.zero }, false).IsValid,
            "Incomplete routes are rejected");

        var north = new TrafficPath(new[] { new Vector3(0, 5.5f, -3), new Vector3(0, 5.5f, 3) }, false, 0.25f);
        var south = new TrafficPath(new[] { new Vector3(0, 5.5f, 3), new Vector3(0, 5.5f, -3) }, false, 0.25f);
        Expect(Near(north.Position(3).x, 0.25f) && Near(south.Position(3).x, -0.25f) && Near(north.Position(3).y, 5.5f),
            "Opposite directions on a centre line become separate right-hand lanes without changing road height");
        var canPass = true;
        for (var step = 0; step <= 120; step++)
        {
            var distance = step * 0.05f;
            var a = new TrafficPose(north.Position(distance), 0f);
            var b = new TrafficPose(south.Position(distance), 180f);
            if (Box(a).Overlaps(Box(b))) canPass = false;
        }
        Expect(canPass, "Opposing cars pass along the same road without overlapping their safety footprints");
        var corner = new TrafficPath(new[] { Vector3.zero, new Vector3(0, 0, 2), new Vector3(2, 0, 2) }, false, 0.25f);
        Expect(Near(corner.PointPosition(1).x, 0.25f) && Near(corner.PointPosition(1).z, 1.75f),
            "A right-angle bend keeps a consistent lane offset on both adjoining segments");
        var offsetLoop = new TrafficPath(new[] { Vector3.zero, new Vector3(0, 0, 3), new Vector3(3, 0, 3), new Vector3(3, 0, 0) }, true, 0.25f);
        Expect(Vector3.Distance(offsetLoop.Position(offsetLoop.Length), offsetLoop.PointPosition(0)) < 0.0001f && Near(offsetLoop.Length, 10f),
            "The offset loop joins correctly at its closing corner");
        var reversing = new TrafficPath(new[] { Vector3.zero, Vector3.forward, Vector3.zero, Vector3.zero }, false, 0.25f);
        var finite = true;
        for (var i = 0; i < reversing.PointCount; i++)
        {
            var p = reversing.PointPosition(i);
            if (float.IsNaN(p.x) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.z)) finite = false;
        }
        Expect(finite, "Reversing and repeated waypoints do not produce invalid offset positions");

        Expect(Box(Pose(0f, 0f)).Overlaps(Box(Pose(0f, 0.3f))),
            "Overlapping car bodies are detected");
        Expect(!Box(Pose(0f, 0f)).Overlaps(Box(Pose(0.3f, 0f, 180f))),
            "Cars in separate narrow lanes can pass side by side");
        Expect(Box(Pose(0f, 0f)).Overlaps(Box(Pose(0.215f, 0f))) &&
               !Box(Pose(0f, 0f), 0f).Overlaps(Box(Pose(0.215f, 0f), 0f)),
            "Safety clearance blocks near contact before the actual models overlap");
        Expect(!Box(Pose(0f, 0f)).Overlaps(Box(Pose(0f, 0f, 0f, 2f))),
            "Vehicles on vertically separated roads do not block each other");

        var from = Pose(-2f, 0f, 90f);
        var to = Pose(2f, 0f, 90f);
        var parked = Box(Pose(0f, 0f));
        Expect(!Box(from).Overlaps(parked) && !Box(to).Overlaps(parked) && Sweep(from, to).Overlaps(parked),
            "Swept checks catch a high-speed car tunneling through another car between endpoints");

        var space = new TrafficReservations();
        space.Occupy(1, Box(Pose(-1f, 0f, 90f)));
        space.Occupy(2, Box(Pose(0f, -1f)));
        var horizontal = Sweep(Pose(-1f, 0f, 90f), Pose(1f, 0f, 90f));
        var vertical = Sweep(Pose(0f, -1f), Pose(0f, 1f));
        Expect(space.IsClear(1, horizontal), "An unoccupied crossing can grant the first movement");
        space.Occupy(1, horizontal);
        Expect(!space.IsClear(2, vertical), "A second crossing movement cannot pass through a car's same-tick path");
        Expect(!space.IsClear(3, Box(Pose(-1f, 0f, 90f))), "A spawn cannot occupy a moving car's reserved path");
        space.Release(1);
        Expect(space.IsClear(3, Box(Pose(-1f, 0f, 90f))), "Recycling a car releases its reservation");

        var gate = new TrafficGate();
        Expect(gate.TryReserve(1) && !gate.TryReserve(2) && gate.Allows(1),
            "An intersection grants entry to one car and rejects another");
        gate.Release(2);
        Expect(!gate.Allows(2), "Another car cannot release the intersection owner's reservation");
        gate.Release(1);
        Expect(gate.TryReserve(2), "The crossing becomes available when its owner clears it");

        var random = new System.Random(1297);
        var containsAllCorners = true;
        for (var test = 0; test < 250 && containsAllCorners; test++)
        {
            var start = Pose((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble() * 360f);
            var end = Pose(start.Position.x + (float)random.NextDouble() - 0.5f,
                start.Position.z + (float)random.NextDouble() - 0.5f, start.Yaw + (float)random.NextDouble() * 150f - 75f);
            var offset = new Vector3(0.08f, 0.1f, -0.06f);
            var swept = TrafficFootprint.Swept(start, end, offset, HalfSize, 0f);
            for (var sample = 0; sample <= 10 && containsAllCorners; sample++)
            {
                var t = sample / 10f;
                var pose = new TrafficPose(Vector3.Lerp(start.Position, end.Position, t),
                    start.Yaw + Mathf.DeltaAngle(start.Yaw, end.Yaw) * t);
                for (var x = -1; x <= 1; x += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var point = pose.Position + pose.Rotation * (offset + new Vector3(HalfSize.x * x, 0, HalfSize.z * z));
                    if (!swept.Overlaps(new TrafficFootprint(point, Vector3.one * 0.0001f, 0f))) containsAllCorners = false;
                }
            }
        }
        Expect(containsAllCorners, "Swept boxes contain sampled intermediate rotations, translations, and offset model centers");

        var cars = new List<Car>();
        var flowPath = new TrafficPath(new[] { new Vector3(0, 0, 1.5f), new Vector3(0, 0, 3),
            new Vector3(3, 0, 3), new Vector3(3, 0, 0), Vector3.zero }, true);
        var zones = new[] { new TrafficFootprint(new Vector3(0, 0, 0), new Vector3(0.5f, 1f, 0.5f), 0f),
            new TrafficFootprint(new Vector3(0, 0, 3), new Vector3(0.5f, 1f, 0.5f), 0f),
            new TrafficFootprint(new Vector3(3, 0, 3), new Vector3(0.5f, 1f, 0.5f), 0f),
            new TrafficFootprint(new Vector3(3, 0, 0), new Vector3(0.5f, 1f, 0.5f), 0f) };
        var gates = new[] { new TrafficGate(), new TrafficGate(), new TrafficGate(), new TrafficGate() };
        var noOverlaps = true;
        var travelled = 0f;
        for (var tick = 0; tick < 20000 && noOverlaps; tick++)
        {
            space.BeginStep();
            foreach (var car in cars) space.Occupy(car.Id, Box(car.Pose));
            for (var i = 0; i < gates.Length; i++)
            {
                if (gates[i].Owner == 0) continue;
                var owner = cars.Find(c => c.Id == gates[i].Owner);
                if (owner == null || !zones[i].Overlaps(Box(owner.Pose))) gates[i].Clear();
            }
            if (tick % 100 == 0 && cars.Count < 8 && space.IsClear(999, Box(Pose(0, 1.5f))))
            {
                var car = new Car { Id = cars.Count + 1, Pose = Pose(0, 1.5f), Speed = 0.45f + cars.Count * 0.04f };
                cars.Add(car);
                space.Occupy(car.Id, Box(car.Pose));
            }
            for (var index = 0; index < cars.Count; index++)
            {
                var car = cars[(index + tick) % cars.Count];
                var advance = Math.Min(car.Speed * 0.02f, flowPath.DistanceToCorner(car.Distance));
                var distance = car.Distance + advance;
                var direction = flowPath.Direction(distance);
                var yaw = (float)(Math.Atan2(direction.x, direction.z) * 180.0 / Math.PI);
                var candidate = new TrafficPose(flowPath.Position(distance), Mathf.MoveTowardsAngle(car.Pose.Yaw, yaw, 180f * 0.02f));
                var swept = Sweep(car.Pose, candidate);
                if (!space.IsClear(car.Id, swept)) continue;
                var gateAllows = true;
                for (var i = 0; i < gates.Length; i++)
                    if (zones[i].Overlaps(swept) && !gates[i].Allows(car.Id)) gateAllows = false;
                if (!gateAllows) continue;
                for (var i = 0; i < gates.Length; i++) if (zones[i].Overlaps(swept)) gates[i].TryReserve(car.Id);
                space.Occupy(car.Id, swept);
                car.Pose = candidate;
                car.Distance = flowPath.NormalizeDistance(distance);
                travelled += advance;
            }
            for (var i = 0; i < cars.Count; i++)
            for (var j = i + 1; j < cars.Count; j++)
                if (Box(cars[i].Pose, 0f).Overlaps(Box(cars[j].Pose, 0f))) noOverlaps = false;
        }
        Expect(noOverlaps && cars.Count == 8 && travelled > 100f,
            "Eight different-speed cars complete a 20,000-step loop simulation without overlap or a global standstill");
        Console.WriteLine(_checks + " checks passed; simulated travel = " + travelled.ToString("F1") + " world units.");
    }
}

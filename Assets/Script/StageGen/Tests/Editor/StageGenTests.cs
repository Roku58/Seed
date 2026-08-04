using System.Collections.Generic;
using NUnit.Framework;
using Seed.Core;
using UnityEngine;

namespace Seed.StageGen.Tests
{
    /// <summary>Seed.StageGen（決定的生成・連結性・テンプレ・バイオーム・配置）のテスト。すべて純C#。</summary>
    public sealed class StageGenTests
    {
        /// <summary>テスト用バイオーム。</summary>
        private static readonly BiomeId Grass = new BiomeId(1);

        /// <summary>テスト用バイオーム。</summary>
        private static readonly BiomeId Lava = new BiomeId(2);

        /// <summary>迷路の標準パイプライン（配置込み）を組む。</summary>
        private static GenerationPipeline BuildMazePipeline(int braid = 0)
        {
            return new GenerationPipeline()
                .Add(new FillPass(CellType.Wall))
                .Add(new MazeCarvePass(braid))
                .Add(new PlayerSpawnPass())
                .Add(new EnemyPlacementPass(2, 400, 1000, new PlacementTable().Add(2, 1000)))
                .Add(new LandmarkPlacementPass(PlacementKind.Exit, 0, LandmarkRule.Farthest))
                .Add(new LandmarkPlacementPass(PlacementKind.Chest, 10, LandmarkRule.RandomWalkable));
        }

        /// <summary>街の標準パイプラインを組む。</summary>
        private static GenerationPipeline BuildTownPipeline()
        {
            return new GenerationPipeline()
                .Add(new FillPass(CellType.Floor))
                .Add(new BspDistrictPass(minDistrictSize: 7))
                .Add(new BuildingPlacementPass())
                .Add(BiomeAssignPass.RegionBased(Grass, Lava))
                .Add(new PlayerSpawnPass());
        }

        /// <summary>グリッドの全セル（3レイヤー）と配置が一致するか。</summary>
        private static void AssertBlueprintEquals(StageBlueprint a, StageBlueprint b)
        {
            Assert.AreEqual(a.Grid.Width, b.Grid.Width);
            Assert.AreEqual(a.Grid.Height, b.Grid.Height);
            for (var y = 0; y < a.Grid.Height; y++)
            {
                for (var x = 0; x < a.Grid.Width; x++)
                {
                    Assert.AreEqual(a.Grid.Get(x, y).Value, b.Grid.Get(x, y).Value, $"cell({x},{y})");
                    Assert.AreEqual(a.Grid.GetVariant(x, y), b.Grid.GetVariant(x, y), $"variant({x},{y})");
                    Assert.AreEqual(a.Grid.GetBiome(x, y).Value, b.Grid.GetBiome(x, y).Value, $"biome({x},{y})");
                }
            }
            Assert.AreEqual(a.Placements.Count, b.Placements.Count);
            for (var i = 0; i < a.Placements.Count; i++)
            {
                Assert.AreEqual(a.Placements[i].Kind.Value, b.Placements[i].Kind.Value);
                Assert.AreEqual(a.Placements[i].RefId, b.Placements[i].RefId);
                Assert.AreEqual(a.Placements[i].X, b.Placements[i].X);
                Assert.AreEqual(a.Placements[i].Y, b.Placements[i].Y);
            }
        }

        /// <summary>全歩行可能セルが相互到達可能か（起点からのBFSで全域に届く）。</summary>
        private static void AssertFullyConnected(StageGrid grid)
        {
            Assert.IsTrue(grid.TryFindFirstWalkable(out var sx, out var sy));
            var distances = grid.ComputeDistances(sx, sy);
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.Get(x, y).IsWalkable)
                    {
                        Assert.GreaterOrEqual(distances[y * grid.Width + x], 0,
                            $"({x},{y}) が孤立している");
                    }
                }
            }
        }

        /// <summary>決定性: 同seed→3レイヤー＋配置まで完全一致 / 異seed→相違。</summary>
        [Test]
        public void Generation_IsDeterministic()
        {
            var a = BuildMazePipeline(150).Generate(21, 21, 1.5f, seed: 42);
            var b = BuildMazePipeline(150).Generate(21, 21, 1.5f, seed: 42);
            AssertBlueprintEquals(a, b);

            var c = BuildMazePipeline(150).Generate(21, 21, 1.5f, seed: 43);
            var different = false;
            for (var y = 0; y < a.Grid.Height && !different; y++)
            {
                for (var x = 0; x < a.Grid.Width && !different; x++)
                {
                    different = a.Grid.Get(x, y).Value != c.Grid.Get(x, y).Value;
                }
            }
            Assert.IsTrue(different, "異なるシードは異なる地形になる");
        }

        /// <summary>迷路: 全域連結（どの床からどの床へも到達できる）。</summary>
        [Test]
        public void Maze_IsFullyConnected()
        {
            var blueprint = BuildMazePipeline().Generate(21, 21, 1f, seed: 7);
            AssertFullyConnected(blueprint.Grid);
        }

        /// <summary>braid: 0なら行き止まりがあり（完全迷路）、上げると減る。</summary>
        [Test]
        public void Maze_Braid_ReducesDeadEnds()
        {
            var perfect = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall)).Add(new MazeCarvePass(0))
                .Generate(21, 21, 1f, seed: 7);
            var braided = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall)).Add(new MazeCarvePass(1000))
                .Generate(21, 21, 1f, seed: 7);

            var deadEndsPerfect = CountDeadEnds(perfect.Grid);
            var deadEndsBraided = CountDeadEnds(braided.Grid);
            Assert.Greater(deadEndsPerfect, 0, "完全迷路には行き止まりがある");
            Assert.Less(deadEndsBraided, deadEndsPerfect, "braid で行き止まりが減る");
            AssertFullyConnected(braided.Grid);
        }

        /// <summary>街: 道路が連結し、区画が記録され、建物に出入口がある。</summary>
        [Test]
        public void Town_RoadsConnected_BuildingsHaveDoors()
        {
            var blueprint = BuildTownPipeline().Generate(31, 31, 1f, seed: 9);
            var grid = blueprint.Grid;

            Assert.Greater(blueprint.Regions.Count, 1, "複数の区画に割られる");
            AssertFullyConnected(grid);

            // 各Doorは歩行可能な隣を持つ（塞がれた出入口が無い）
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (!grid.Get(x, y).Equals(CellType.Door))
                    {
                        continue;
                    }
                    var open = grid.Get(x + 1, y).IsWalkable || grid.Get(x - 1, y).IsWalkable
                        || grid.Get(x, y + 1).IsWalkable || grid.Get(x, y - 1).IsWalkable;
                    Assert.IsTrue(open, $"Door({x},{y}) が塞がれている");
                }
            }
            Assert.Greater(grid.CountOf(CellType.Door), 0, "建物に出入口がある");
        }

        /// <summary>配置: 起点・敵・出口・宝箱が存在し、歩行可能セル上で、重ならない。</summary>
        [Test]
        public void Placements_AreOnWalkableCells_WithoutOverlap()
        {
            var blueprint = BuildMazePipeline().Generate(21, 21, 1f, seed: 11);

            Assert.IsTrue(blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out _));
            Assert.IsTrue(blueprint.TryFindPlacement(PlacementKind.Exit, out _));
            Assert.IsTrue(blueprint.TryFindPlacement(PlacementKind.Chest, out _));

            var seen = new HashSet<(int, int)>();
            for (var i = 0; i < blueprint.Placements.Count; i++)
            {
                var placement = blueprint.Placements[i];
                Assert.IsTrue(blueprint.Grid.Get(placement.X, placement.Y).IsWalkable,
                    $"{placement.Kind} が歩行不能セル上にある");
                Assert.IsTrue(seen.Add((placement.X, placement.Y)), "配置が重なっている");
            }
        }

        /// <summary>敵の距離帯: 近い帯を指定したら起点近く、遠い帯なら遠くに置かれる。</summary>
        [Test]
        public void EnemyPlacement_RespectsDistanceBand()
        {
            var table = new PlacementTable().Add(2, 1000);
            var nearPipeline = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall)).Add(new MazeCarvePass())
                .Add(new PlayerSpawnPass())
                .Add(new EnemyPlacementPass(3, 0, 300, table));
            var farPipeline = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall)).Add(new MazeCarvePass())
                .Add(new PlayerSpawnPass())
                .Add(new EnemyPlacementPass(3, 700, 1000, table));

            var near = MaxEnemyPermille(nearPipeline.Generate(21, 21, 1f, 5));
            var far = MinEnemyPermille(farPipeline.Generate(21, 21, 1f, 5));
            Assert.LessOrEqual(near, 300, "近い帯の敵は起点近く");
            Assert.GreaterOrEqual(far, 700, "遠い帯の敵は最奥側");
        }

        /// <summary>出現テーブル: 重み付き抽選が決定的で、重み0は選ばれない。</summary>
        [Test]
        public void PlacementTable_WeightedPick_IsDeterministic()
        {
            var table = new PlacementTable().Add(101, 500).Add(102, 500).Add(999, 0);
            var a = new List<int>();
            var b = new List<int>();
            var randomA = new DeterministicRandom(3);
            var randomB = new DeterministicRandom(3);
            for (var i = 0; i < 50; i++)
            {
                a.Add(table.Pick(randomA));
                b.Add(table.Pick(randomB));
            }
            CollectionAssert.AreEqual(a, b, "同シードで同じ列");
            CollectionAssert.DoesNotContain(a, 999, "重み0は絶対に出ない");
            CollectionAssert.Contains(a, 101);
            CollectionAssert.Contains(a, 102);
        }

        /// <summary>テンプレート: 凡例どおりに解釈され、スタンプ後も連結し、内包配置物が載る。</summary>
        [Test]
        public void RoomTemplate_ParseAndStamp_KeepsConnectivity()
        {
            var legend = new RoomTemplateLegend()
                .Cell('#', CellType.Wall)
                .Cell('.', CellType.Floor)
                .Door('D')
                .Placement('C', CellType.Floor, PlacementKind.Chest, refId: 77);
            var vault = RoomTemplate.Parse(new[]
            {
                "#####",
                "#C..#",
                "#...D",
                "#####",
            }, legend);

            Assert.AreEqual(5, vault.Width);
            Assert.AreEqual(4, vault.Height);
            Assert.AreEqual(1, vault.Placements.Count);
            Assert.AreEqual(77, vault.Placements[0].RefId);
            Assert.AreEqual(1, vault.DoorIndices.Count);

            var blueprint = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall))
                .Add(new MazeCarvePass())
                .Add(new TemplateRoomsPass(new[] { vault }))
                .Generate(21, 21, 1f, seed: 13);

            Assert.IsTrue(blueprint.TryFindPlacement(PlacementKind.Chest, out var chest),
                "内包の宝箱が設計図に載る");
            Assert.AreEqual(77, chest.RefId);
            AssertFullyConnected(blueprint.Grid); // Door が通路へ開口され孤立しない
        }

        /// <summary>バイオーム: 距離帯塗りは起点近くと最奥で帯が変わり、区画塗りは区画単位。</summary>
        [Test]
        public void BiomeAssign_DistanceBands_PaintNearAndFar()
        {
            var blueprint = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall))
                .Add(new MazeCarvePass())
                .Add(new PlayerSpawnPass())
                .Add(BiomeAssignPass.DistanceBands(
                    new BiomeAssignPass.Band(500, Grass),
                    new BiomeAssignPass.Band(1000, Lava)))
                .Generate(21, 21, 1f, seed: 17);

            Assert.IsTrue(blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var spawn));
            Assert.AreEqual(Grass.Value, blueprint.Grid.GetBiome(spawn.X, spawn.Y).Value, "起点は近帯");

            // 最遠点は遠帯
            var distances = blueprint.Grid.ComputeDistances(spawn.X, spawn.Y);
            var bestIndex = 0;
            for (var i = 0; i < distances.Length; i++)
            {
                if (distances[i] > distances[bestIndex])
                {
                    bestIndex = i;
                }
            }
            Assert.AreEqual(Lava.Value, blueprint.Grid.GetBiome(
                bestIndex % blueprint.Grid.Width, bestIndex / blueprint.Grid.Width).Value, "最奥は遠帯");
        }

        /// <summary>バリアント: 抽選が決定的で、重み0は選ばれず、表に無い組は0になる。</summary>
        [Test]
        public void TileVariant_Deterministic_AndRespectsWeights()
        {
            var table = new TileVariantTable()
                .Add(BiomeId.None, CellType.Wall, 0, 500, 500); // バリアント0は重み0
            GenerationPipeline Build() => new GenerationPipeline()
                .Add(new FillPass(CellType.Wall))
                .Add(new MazeCarvePass())
                .Add(new TileVariantPass(table));

            var a = Build().Generate(15, 15, 1f, seed: 21);
            var b = Build().Generate(15, 15, 1f, seed: 21);
            AssertBlueprintEquals(a, b);

            for (var y = 0; y < a.Grid.Height; y++)
            {
                for (var x = 0; x < a.Grid.Width; x++)
                {
                    if (a.Grid.Get(x, y).Equals(CellType.Wall))
                    {
                        Assert.AreNotEqual(0, a.Grid.GetVariant(x, y), "重み0のバリアントは選ばれない");
                    }
                    else
                    {
                        Assert.AreEqual(0, a.Grid.GetVariant(x, y), "表に無いセル種別は既定の0");
                    }
                }
            }
        }

        /// <summary>Forkの独立性: パスを末尾に追加しても先行パスの出力（地形）は不変。</summary>
        [Test]
        public void Pipeline_AppendingPass_DoesNotDisturbEarlierPasses()
        {
            var baseline = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall)).Add(new MazeCarvePass())
                .Generate(21, 21, 1f, seed: 31);
            var extended = new GenerationPipeline()
                .Add(new FillPass(CellType.Wall)).Add(new MazeCarvePass())
                .Add(new PlayerSpawnPass())
                .Add(new LandmarkPlacementPass(PlacementKind.Exit, 0, LandmarkRule.Farthest))
                .Generate(21, 21, 1f, seed: 31);

            for (var y = 0; y < baseline.Grid.Height; y++)
            {
                for (var x = 0; x < baseline.Grid.Width; x++)
                {
                    Assert.AreEqual(baseline.Grid.Get(x, y).Value, extended.Grid.Get(x, y).Value,
                        "後続パスを足しても迷路の形は変わらない（Forkの独立性）");
                }
            }
        }

        /// <summary>GridToWorld: 中心原点・セルサイズ倍率で変換される。</summary>
        [Test]
        public void GridToWorld_CentersStage()
        {
            var blueprint = new StageBlueprint(21, 21, cellSize: 1.5f, seed: 1);
            Assert.AreEqual(Vector3.zero, blueprint.GridToWorld(10, 10), "中央セルが原点");
            var corner = blueprint.GridToWorld(0, 0);
            Assert.AreEqual(-15f, corner.x, 0.001f);
            Assert.AreEqual(-15f, corner.z, 0.001f);
        }

        /// <summary>行き止まり（歩行可能な隣が1つ以下）の数を数える。</summary>
        private static int CountDeadEnds(StageGrid grid)
        {
            var count = 0;
            for (var y = 1; y < grid.Height - 1; y++)
            {
                for (var x = 1; x < grid.Width - 1; x++)
                {
                    if (!grid.Get(x, y).IsWalkable)
                    {
                        continue;
                    }
                    var open = 0;
                    if (grid.Get(x + 1, y).IsWalkable) { open++; }
                    if (grid.Get(x - 1, y).IsWalkable) { open++; }
                    if (grid.Get(x, y + 1).IsWalkable) { open++; }
                    if (grid.Get(x, y - 1).IsWalkable) { open++; }
                    if (open <= 1)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>敵配置の距離‰の最大値。</summary>
        private static int MaxEnemyPermille(StageBlueprint blueprint)
        {
            return EnemyPermilles(blueprint, out var max, out _) ? max : -1;
        }

        /// <summary>敵配置の距離‰の最小値。</summary>
        private static int MinEnemyPermille(StageBlueprint blueprint)
        {
            return EnemyPermilles(blueprint, out _, out var min) ? min : -1;
        }

        /// <summary>敵配置の距離‰（最大・最小）を計算する。</summary>
        private static bool EnemyPermilles(StageBlueprint blueprint, out int max, out int min)
        {
            max = -1;
            min = int.MaxValue;
            Assert.IsTrue(blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var spawn));
            var distances = blueprint.Grid.ComputeDistances(spawn.X, spawn.Y);
            var maxDistance = 1;
            for (var i = 0; i < distances.Length; i++)
            {
                if (distances[i] > maxDistance)
                {
                    maxDistance = distances[i];
                }
            }
            var found = false;
            for (var i = 0; i < blueprint.Placements.Count; i++)
            {
                var placement = blueprint.Placements[i];
                if (!placement.Kind.Equals(PlacementKind.EnemySpawn))
                {
                    continue;
                }
                found = true;
                var permille = distances[placement.Y * blueprint.Grid.Width + placement.X]
                    * 1000 / maxDistance;
                if (permille > max) { max = permille; }
                if (permille < min) { min = permille; }
            }
            Assert.IsTrue(found, "敵が配置されている");
            return true;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match;
using osu.Game.Screens.OnlinePlay.Multiplayer.Participants;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Multiplayer
{
    public class TestSceneMultiplayerMatchSubScreen : MultiplayerTestScene
    {
        private MultiplayerMatchSubScreen screen;

        private BeatmapManager beatmaps;
        private RulesetStore rulesets;
        private BeatmapSetInfo importedSet;

        public TestSceneMultiplayerMatchSubScreen()
            : base(false)
        {
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            Dependencies.Cache(rulesets = new RulesetStore(Realm));
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, rulesets, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(Realm);

            beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();

            importedSet = beatmaps.GetAllUsableBeatmapSets().First();
        }

        [SetUp]
        public new void Setup() => Schedule(() =>
        {
            SelectedRoom.Value = new Room { Name = { Value = "Test Room" } };
        });

        [SetUpSteps]
        public void SetupSteps()
        {
            AddStep("load match", () => LoadScreen(screen = new MultiplayerMatchSubScreen(SelectedRoom.Value)));
            AddUntilStep("wait for load", () => screen.IsCurrentScreen());
        }

        [Test]
        public void TestCreatedRoom()
        {
            AddStep("add playlist item", () =>
            {
                SelectedRoom.Value.Playlist.Add(new PlaylistItem
                {
                    Beatmap = { Value = new TestBeatmap(new OsuRuleset().RulesetInfo).BeatmapInfo },
                    Ruleset = { Value = new OsuRuleset().RulesetInfo },
                });
            });

            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();

            AddUntilStep("wait for join", () => RoomJoined);
        }

        [Test]
        public void TestTaikoOnlyMod()
        {
            AddStep("add playlist item", () =>
            {
                SelectedRoom.Value.Playlist.Add(new PlaylistItem
                {
                    Beatmap = { Value = new TestBeatmap(new TaikoRuleset().RulesetInfo).BeatmapInfo },
                    Ruleset = { Value = new TaikoRuleset().RulesetInfo },
                    AllowedMods = { new TaikoModSwap() }
                });
            });

            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();

            AddUntilStep("wait for join", () => RoomJoined);

            AddStep("select swap mod", () => Client.ChangeUserMods(API.LocalUser.Value.OnlineID, new[] { new TaikoModSwap() }));
            AddUntilStep("participant panel has mod", () => this.ChildrenOfType<ParticipantPanel>().Any(p => p.ChildrenOfType<ModIcon>().Any(m => m.Mod is TaikoModSwap)));
        }

        [Test]
        public void TestSettingValidity()
        {
            AddAssert("create button not enabled", () => !this.ChildrenOfType<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>().Single().Enabled.Value);

            AddStep("set playlist", () =>
            {
                SelectedRoom.Value.Playlist.Add(new PlaylistItem
                {
                    Beatmap = { Value = new TestBeatmap(new OsuRuleset().RulesetInfo).BeatmapInfo },
                    Ruleset = { Value = new OsuRuleset().RulesetInfo },
                });
            });

            AddAssert("create button enabled", () => this.ChildrenOfType<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>().Single().Enabled.Value);
        }

        [Test]
        public void TestStartMatchWhileSpectating()
        {
            AddStep("set playlist", () =>
            {
                SelectedRoom.Value.Playlist.Add(new PlaylistItem
                {
                    Beatmap = { Value = beatmaps.GetWorkingBeatmap(importedSet.Beatmaps.First()).BeatmapInfo },
                    Ruleset = { Value = new OsuRuleset().RulesetInfo },
                });
            });

            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();

            AddUntilStep("wait for room join", () => RoomJoined);

            AddStep("join other user (ready)", () =>
            {
                Client.AddUser(new APIUser { Id = PLAYER_1_ID });
                Client.ChangeUserState(PLAYER_1_ID, MultiplayerUserState.Ready);
            });

            ClickButtonWhenEnabled<MultiplayerSpectateButton>();

            AddUntilStep("wait for spectating user state", () => Client.LocalUser?.State == MultiplayerUserState.Spectating);

            ClickButtonWhenEnabled<MultiplayerReadyButton>();

            AddUntilStep("match started", () => Client.Room?.State == MultiplayerRoomState.WaitingForLoad);
        }
    }
}

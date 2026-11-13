using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Sts2LanConnect.Scripts;

namespace Sts2LanConnect.ProtocolPlanTests;

public sealed class LanConnectGame113CompatibilityTests
{
    [Theory]
    [InlineData(GameMode.Standard, false, null)]
    [InlineData(GameMode.Standard, true, "custom-seed")]
    [InlineData(GameMode.Daily, false, "daily-seed")]
    public void Compat_join_response_preserves_the_native_body_except_for_player_list_width(
        GameMode mode, bool isCustom, string? seed)
    {
        ClientLobbyJoinResponseMessage response = new()
        {
            playersInLobby = [],
            modifiers = [],
            gameMode = mode,
            isCustom = isCustom,
            seed = seed,
            ascension = 17
        };
        PacketWriter native = new();
        response.Serialize(native);

        foreach (int listBits in new[] { 3, 5 })
        {
            PacketWriter compat = new();
            LanConnectSerializationPatches.SerializeJoinResponseBody(compat, response, listBits);
            Assert.Equal(native.BitPosition + listBits - 3, compat.BitPosition);

            PacketReader nativeReader = new();
            nativeReader.Reset(native.Buffer);
            PacketReader compatReader = new();
            compatReader.Reset(compat.Buffer);
            Assert.Empty(nativeReader.ReadList<StartRunLobbyPlayer>(3));
            Assert.Empty(compatReader.ReadList<StartRunLobbyPlayer>(listBits));
            while (nativeReader.BitPosition < native.BitPosition)
            {
                Assert.Equal(nativeReader.ReadBool(), compatReader.ReadBool());
            }
            Assert.Equal(compat.BitPosition, compatReader.BitPosition);

            if (listBits == 3)
            {
                compatReader.Reset(compat.Buffer);
                ClientLobbyJoinResponseMessage decoded = default;
                decoded.Deserialize(compatReader);
                Assert.Equal(mode, decoded.gameMode);
                Assert.Equal(isCustom, decoded.isCustom);
                Assert.Equal(seed, decoded.seed);
                Assert.Equal(17, decoded.ascension);
            }
        }
    }

    [Theory]
    [InlineData(GameMode.Standard, false)]
    [InlineData(GameMode.Standard, true)]
    [InlineData(GameMode.Daily, false)]
    public void Join_result_gets_mode_and_custom_flag_from_each_response(GameMode mode, bool isCustom)
    {
        ClientLobbyJoinResponseMessage lobbyResponse = new() { gameMode = mode, isCustom = isCustom };
        var lobby = LanConnectLobbyManagedJoinFlow.CreateJoinResult(lobbyResponse);
        Assert.Equal(RunSessionState.InLobby, lobby.sessionState);
        Assert.Equal(mode, lobby.gameMode);
        Assert.Equal(isCustom, lobby.isCustom);
        Assert.True(lobby.joinResponse.HasValue);

        SerializableRun run = new() { GameMode = mode, IsCustomGame = isCustom };
        ClientLoadJoinResponseMessage loadResponse = new() { serializableRun = run };
        var load = LanConnectLobbyManagedJoinFlow.CreateJoinResult(loadResponse);
        Assert.Equal(RunSessionState.InLoadedLobby, load.sessionState);
        Assert.Equal(mode, load.gameMode);
        Assert.Equal(isCustom, load.isCustom);
        Assert.Same(run, load.loadJoinResponse!.Value.serializableRun);

        ClientRejoinResponseMessage rejoinResponse = new() { serializableRun = run };
        var rejoin = LanConnectLobbyManagedJoinFlow.CreateJoinResult(rejoinResponse);
        Assert.Equal(RunSessionState.Running, rejoin.sessionState);
        Assert.Equal(mode, rejoin.gameMode);
        Assert.Equal(isCustom, rejoin.isCustom);
        Assert.Same(run, rejoin.rejoinResponse!.Value.serializableRun);
    }

    [Fact]
    public void Saved_custom_run_without_modifiers_keeps_the_custom_room_label()
    {
        SerializableRun run = new() { GameMode = GameMode.Standard, IsCustomGame = true, Modifiers = [] };
        Assert.Equal("custom", LanConnectMultiplayerSaveRoomBinding.GetLobbyGameMode(run));
        run.IsCustomGame = false;
        Assert.Equal("standard", LanConnectMultiplayerSaveRoomBinding.GetLobbyGameMode(run));
    }

    [Fact]
    public void Scaling_transpiler_still_rewrites_the_real_game_player_count_read()
    {
        MethodInfo target = AccessTools.Method(typeof(MultiplayerScalingModel), "ModifyBlockMultiplicative");
        List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target);
        MethodInfo transpiler = AccessTools.Method(typeof(DifficultyScalingPatches), "PatchPlayersCountTranspiler");
        var patched = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, [original])!).ToList();
        MethodInfo helper = AccessTools.Method(typeof(DifficultyScalingPatches), "GetEffectivePlayerCount");
        Assert.Single(patched, instruction => instruction.opcode == OpCodes.Call && Equals(instruction.operand, helper));
    }

    [Fact]
    public void Capacity_patch_matches_the_game_constructor_with_the_custom_flag()
    {
        Assert.NotNull(AccessTools.Constructor(typeof(StartRunLobby),
            [typeof(GameMode), typeof(bool), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int)]));
    }
}

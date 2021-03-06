﻿using CustomAvatar.Avatar;
using CustomAvatar.Player;
using MultiplayerExtensions.Packets;
using System;
using System.Collections.Generic;
using System.Threading;
using Zenject;

namespace MultiplayerAvatars.Avatars
{
    class CustomAvatarManager : IInitializable
    {
        [Inject]
        private IMultiplayerSessionManager _sessionManager;

        [Inject]
        private PacketManager _packetManager;

        [Inject]
        private AvatarSpawner _avatarSpawner;

        [Inject]
        private PlayerAvatarManager _avatarManager;

        [Inject]
        private VRPlayerInput _playerInput;

        [Inject]
        private FloorController _floorController;

        [Inject]
        private IAvatarProvider<LoadedAvatar> _avatarProvider;

        private PacketSerializer _serializer = new PacketSerializer();

        public Action<IConnectedPlayer, CustomAvatarData> avatarReceived;
        public CustomAvatarData localAvatar = new CustomAvatarData();
        private Dictionary<string, CustomAvatarData> _avatars = new Dictionary<string, CustomAvatarData>();

        public void Initialize()
        {
            Plugin.Log?.Info("Setting up CustomAvatarManager");
            _packetManager.RegisterSerializer(_serializer);

            _avatarManager.avatarChanged += OnAvatarChanged;
            _avatarManager.avatarScaleChanged += delegate(float scale) { localAvatar.scale = scale; };
            _floorController.floorPositionChanged += delegate (float floor) { localAvatar.floor = floor; };

            _sessionManager.connectedEvent += SendLocalAvatarPacket;
            _sessionManager.playerConnectedEvent += OnPlayerConnected;
            _serializer.RegisterCallback(HandleAvatarPacket, CustomAvatarPacket.pool.Obtain);

            OnAvatarChanged(_avatarManager.currentlySpawnedAvatar);
            localAvatar.floor = _floorController.floorPosition;
        }

        public CustomAvatarData? GetAvatarByUserId(string userId)
        {
            if (_avatars.ContainsKey(userId))
                return _avatars[userId];
            return null;
        }

        private void OnAvatarChanged(SpawnedAvatar avatar)
        {
            if (!avatar) return;
            _avatarProvider.HashAvatar(avatar.avatar).ContinueWith(r =>
            {
                localAvatar.hash = r.Result;
                localAvatar.scale = avatar.scale;
            });
        }

        private void OnPlayerConnected(IConnectedPlayer player)
        {
            SendLocalAvatarPacket();
        }

        private void SendLocalAvatarPacket()
        {
            CustomAvatarPacket localAvatarPacket = localAvatar.GetPacket();
            Plugin.Log?.Info($"Sending 'CustomAvatarPacket' with {localAvatar.hash}");
            _sessionManager.Send(localAvatarPacket);
        }

        private void HandleAvatarPacket(CustomAvatarPacket packet, IConnectedPlayer player)
        {
            Plugin.Log?.Info($"Received 'CustomAvatarPacket' from '{player.userId}' with '{packet.hash}'");
            _avatars[player.userId] = new CustomAvatarData(packet);
            avatarReceived?.Invoke(player, _avatars[player.userId]);
        }
    }
}

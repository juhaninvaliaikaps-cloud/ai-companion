using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>One furniture picture from the art catalogue (JUH-225's room_catalog.art.json).</summary>
    [Serializable]
    public sealed class RoomPiece
    {
        public string id;
        public string file;
        public int[] sizePx;
        public int[] anchorPx;
        public string placement;
        public bool walkable;
        public string variantOf;
        public int[] defaultAnchorRoomPx;

        public bool IsVariant => !string.IsNullOrEmpty(variantOf);
        public bool HasDefaultSpot => defaultAnchorRoomPx != null && defaultAnchorRoomPx.Length == 2;
        public bool OnWall => placement == "wall";
        public bool OnSurface => placement == "surface";

        /// <summary>The Resources path without the extension, e.g. "HerRoom/furniture/room_bed".</summary>
        public string ResourcePath
        {
            get
            {
                var path = file ?? string.Empty;
                if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    path = path.Substring(0, path.Length - 4);
                return "HerRoom/" + path;
            }
        }
    }

    /// <summary>
    /// JUH-229: the furniture data from ChatGPT's art pack. Pixel positions are on the 1024 px room
    /// shell, y down; a floor piece's anchor is where it touches the floor at the front. The view
    /// places each picture with <see cref="RectOf"/> and draws it back to front by <see cref="DepthOf"/>.
    /// </summary>
    public sealed class RoomCatalog
    {
        public const float RoomPixels = 1024f;

        /// <summary>Rugs lie under everything; wall pieces hang behind everything on the floor.</summary>
        public const float RugDepth = -1f;
        public const float WallDepth = -0.5f;

        [Serializable]
        sealed class Data
        {
            public RoomPiece[] furniture;
        }

        public readonly RoomPiece[] Pieces;

        RoomCatalog(RoomPiece[] pieces)
        {
            Pieces = pieces ?? new RoomPiece[0];
        }

        public static RoomCatalog Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new RoomCatalog(null);
            try
            {
                var data = JsonUtility.FromJson<Data>(json);
                return new RoomCatalog(data != null ? data.furniture : null);
            }
            catch (ArgumentException)
            {
                return new RoomCatalog(null);
            }
        }

        /// <summary>The pieces of the default room: each with its own spot, no alternates (the open wardrobe, the shared bed).</summary>
        public List<RoomPiece> DefaultRoom()
        {
            var list = new List<RoomPiece>();
            foreach (var piece in Pieces)
            {
                if (piece != null && piece.HasDefaultSpot && !piece.IsVariant && Valid(piece))
                    list.Add(piece);
            }

            return list;
        }

        static bool Valid(RoomPiece piece)
        {
            return piece.sizePx != null && piece.sizePx.Length == 2 && piece.anchorPx != null && piece.anchorPx.Length == 2
                && piece.sizePx[0] > 0 && piece.sizePx[1] > 0 && !string.IsNullOrEmpty(piece.file);
        }

        /// <summary>The picture's rectangle in room fractions: x, y (top), width, height; y down.</summary>
        public static Rect RectOf(RoomPiece piece)
        {
            var left = piece.defaultAnchorRoomPx[0] - piece.anchorPx[0];
            var top = piece.defaultAnchorRoomPx[1] - piece.anchorPx[1];
            return new Rect(left / RoomPixels, top / RoomPixels, piece.sizePx[0] / RoomPixels, piece.sizePx[1] / RoomPixels);
        }

        /// <summary>
        /// How far forward the piece stands (room fraction, y down): the y of its floor contact.
        /// A character whose feet are lower on the floor is drawn over it. A surface piece (a pot on
        /// the wardrobe) takes the depth of the piece it stands on, just in front of it.
        /// </summary>
        public static float DepthOf(RoomPiece piece, IList<RoomPiece> room)
        {
            if (piece.walkable && !piece.OnWall)
                return RugDepth;
            if (piece.OnWall)
                return WallDepth;
            var own = piece.defaultAnchorRoomPx[1] / RoomPixels;
            if (!piece.OnSurface || room == null)
                return own;

            var x = piece.defaultAnchorRoomPx[0] / RoomPixels;
            foreach (var under in room)
            {
                if (under == piece || under.OnSurface || under.OnWall || under.walkable)
                    continue;
                var rect = RectOf(under);
                if (x >= rect.xMin && x <= rect.xMax && own >= rect.yMin && own <= rect.yMax)
                    return under.defaultAnchorRoomPx[1] / RoomPixels + 0.0001f;
            }

            return own;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
namespace TestMod
{
    public class BodyChunkHook
    {
        public static void AddHook()
        {

            On.BodyChunk.Update += BodyChunk_Update;
            On.BodyChunk.checkAgainstSlopesVertically += BodyChunk_checkAgainstSlopesVertically;
            On.BodyChunk.CheckVerticalCollision += BodyChunk_CheckVerticalCollision;
            On.BodyChunk.CheckHorizontalCollision += BodyChunk_CheckHorizontalCollision;
        }

        private static void BodyChunk_Update(On.BodyChunk.orig_Update orig, BodyChunk self)
        {
           
            orig(self);
            if (RoomPhysics.Get(self.owner.room).TryGetObject(self.owner, out GameObject _))
            {
                self.vel *= 0;
                
            }



        }

        private static void BodyChunk_CheckHorizontalCollision(On.BodyChunk.orig_CheckHorizontalCollision orig, BodyChunk self)
        {
            orig(self);
        }

        private static void BodyChunk_CheckVerticalCollision(On.BodyChunk.orig_CheckVerticalCollision orig, BodyChunk self)
        {
            orig(self);
            if ( RoomPhysics.Get(self.owner.room).IsPointInAnyRb(self.pos-new Vector2(0,self.TerrainRad-0.1f)))
            {
                self.contactPoint.y = -1;
            }
        }

        private static void BodyChunk_checkAgainstSlopesVertically(On.BodyChunk.orig_checkAgainstSlopesVertically orig, BodyChunk self)
        {
            orig(self);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RWCustom;
using UnityEngine;
namespace TestMod
{
    public class BodyPartHook
    {
        #region Hooking and helper stuff
        public static void AddHooks()
        {
           // On.BodyPart.PushOutOfTerrain += BodyPart_PushOutOfTerrain;
        }


        #endregion
        private static void BodyPart_PushOutOfTerrain(On.BodyPart.orig_PushOutOfTerrain orig, BodyPart self, Room room, UnityEngine.Vector2 basePoint)
        {
            //orig(self, room, basePoint);
            //RoomPhysics rp = RoomPhysics.Get(room);
            //foreach(var item in rp.ObjList)
            //{
            //    if(rp.IsChunkTouchingGameObject(item.Value,self.pos,self.rad)==item.Value.GetComponent<Collider2D>())
            //    {
            //        bool inside = rp.PointInRb(item.Value, self.pos);
            //        Vector2[] point=rp.CloestPointToRb(item.Value, self.pos,self.vel, self.rad);
            //        self.terrainContact = true;
            //        if(inside) 
            //        {
            //            self.pos -= (self.pos - point);

            //        }else
            //        {
            //            self.pos += (self.pos - point);
            //        }
            //        self.vel.y *= 0;
            //        self.vel.x *= self.surfaceFric;
            //    }
            //}
            
        }
    }
}

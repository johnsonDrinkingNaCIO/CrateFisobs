﻿using Rewired.UI.ControlMapper;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TestMod
{
    public class RoomPhysics
    {
        public const float PIXELS_PER_UNIT = 20f;
      
        public const float OBJECT_LAYER = 1 << 2;

        private float water_level = 0;
        private float WATER_LEVEL { get { return water_level/PIXELS_PER_UNIT; } set { water_level = value; } }
        private static readonly Dictionary<Room, RoomPhysics> _systems = new();

        private readonly Room _room;
        private readonly Scene _scene;
        private readonly PhysicsScene2D _physics;
        private readonly Dictionary<UpdatableAndDeletable, GameObject> _linkedObjects = new();

        #region Hooks and Helpers
        public static void AddHooks()
        {
            On.Room.Update += Room_Update;
            On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
            On.AbstractRoom.Abstractize += AbstractRoom_Abstractize;
            //On.Room.GetTile_int_int += Room_GetTile_int_int;
            //On.PhysicalObject.IsTileSolid += PhysicalObject_IsTileSolid;
        }
         
        private static bool PhysicalObject_IsTileSolid(On.PhysicalObject.orig_IsTileSolid orig, PhysicalObject self, int bChunk, int relativeX, int relativeY)
        {
            if(orig(self, bChunk, relativeX, relativeY)) return true;
            if (self.room.ReadyForPlayer)
            {
                foreach (KeyValuePair<UpdatableAndDeletable, GameObject> item in RoomPhysics.Get(self.room)._linkedObjects)
                {
                    if (RoomPhysics.Get(self.room).PointInRb(item.Value, self.bodyChunks[bChunk].pos  + new Vector2(relativeX * 20, relativeY * 20)))
                    {

                        return true;

                    }
                }
            }
            return false;
        }

        public static Room.Tile Room_GetTile_int_int(On.Room.orig_GetTile_int_int orig, Room self, int x, int y)
        {
            
            if (self.ReadyForPlayer)
            {
                var obj = RoomPhysics.Get(self);
                foreach (KeyValuePair<UpdatableAndDeletable, GameObject> item in obj._linkedObjects)
                {
                    if (obj.PointInRb(item.Value, new Vector2(x * 20, y * 20)))
                    {
                        Room.Tile tile = new Room.Tile(x, y, Room.Tile.TerrainType.Solid, false, false, false, 0, 0);
                        return tile;
                    }
                }
            }
            return orig(self, x, y);
        }

        private static void Room_Update(On.Room.orig_Update orig, Room self)
        {


            if (_systems.TryGetValue(self, out var system))
            {
                system.Update();     
                orig(self);
                system.LateUpdate();
            }
            else
            {
                orig(self);
            }

          

        }

        private static void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            orig(self);

            foreach (var system in _systems.Values)
            {
                system.Dispose();
            }

            _systems.Clear();
        }

        private static void AbstractRoom_Abstractize(On.AbstractRoom.orig_Abstractize orig, AbstractRoom self)
        {
            if (self.realizedRoom != null && _systems.TryGetValue(self.realizedRoom, out var system))
            {
                system.Dispose();
                _systems.Remove(self.realizedRoom);
            }

            orig(self);
        }

        public static RoomPhysics Get(Room room)
        {
            if (!_systems.TryGetValue(room, out var system))
            {
                _systems[room] = system = new RoomPhysics(room);
            }

            return system;
        }
        #endregion Hooks and Helpers

        private RoomPhysics(Room room)
        {
            _room = room;
            this.water_level = room.floatWaterLevel;
            // Create a scene with physics independent from the main scene
            _scene = SceneManager.CreateScene($"Physics System {room.abstractRoom.name}", new CreateSceneParameters() { localPhysicsMode = LocalPhysicsMode.Physics2D });
            _physics = _scene.GetPhysicsScene2D();

            // Add room tiles as a collider
            RefreshTiles();
        }

        public bool TryGetObject(UpdatableAndDeletable owner, out GameObject gameObj)
        {
            return _linkedObjects.TryGetValue(owner, out gameObj);
        }

        public GameObject CreateObject(UpdatableAndDeletable owner)
        {
            var obj = new GameObject();
            obj.layer = 1<<2;
            SceneManager.MoveGameObjectToScene(obj, _scene);
            try
            {
                _linkedObjects.Add(owner, obj);
            }
            catch
            {
                UnityEngine.Object.Destroy(obj);
                throw;
            }
            
            return obj;
        }

      

        //Layer 1 is Floor , Layer 2 is everything else
        private void RefreshTiles()
        {
            var obj = new GameObject("Room Geometry");
            obj.layer = (1<<1);
            obj.isStatic = true;
            SceneManager.MoveGameObjectToScene(obj, _scene);

            var rb2d = obj.AddComponent<Rigidbody2D>();
            rb2d.bodyType = RigidbodyType2D.Static;

            var compositeCollider = obj.AddComponent<CompositeCollider2D>();
            compositeCollider.generationType = CompositeCollider2D.GenerationType.Manual;
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Outlines;

            var width = _room.TileWidth;
            var height = _room.TileHeight;
            var tiles = _room.Tiles;

            // Make polygons
            for(int y = 0; y < height; y++)
            {
                int? startX = null;

                for(int x = 0; x <= width; x++)
                {
                    var tile = x < width ? tiles[x, y] : null;
                    if (tile != null && tile.Terrain==Room.Tile.TerrainType.Solid)
                    {
                        if(startX == null)
                        {
                            startX = x;
                        }
                    }
                    else
                    {
                        if(startX != null)
                        {
                            var col = obj.AddComponent<BoxCollider2D>();
                            
                            col.size = new Vector2(x - startX.Value, 1f) * 20f / PIXELS_PER_UNIT;
                            col.offset = new Vector2(startX.Value, y) * 20f / PIXELS_PER_UNIT + col.size / 2f;
                            col.usedByComposite = true;

                            startX = null;
                        }
                    }
                    Room.SlopeDirection slope = _room.IdentifySlope(new IntVector2(x, y));
                    if (tile!=null && slope != Room.SlopeDirection.Broken && tiles[x,y].Terrain==Room.Tile.TerrainType.Slope)
                    {

                        if (slope == Room.SlopeDirection.UpRight)
                        {
                            var col = obj.AddComponent<PolygonCollider2D>();

                            col.points = new Vector2[3] { new Vector2(-1, 1) * 20f / PIXELS_PER_UNIT, new Vector2(-1, -1) * 20f / PIXELS_PER_UNIT, new Vector2(1, -1) * 20f / PIXELS_PER_UNIT };
                            col.offset = new Vector2(x, y) * 20f / PIXELS_PER_UNIT + Vector2.one * 10 / PIXELS_PER_UNIT;
                            col.usedByComposite = true;
                        }
                        else if (slope == Room.SlopeDirection.UpLeft)
                        {
                            var col = obj.AddComponent<PolygonCollider2D>();

                            col.points = new Vector2[3] { new Vector2(1, 1) * 20f / PIXELS_PER_UNIT, new Vector2(-1, -1) * 20f / PIXELS_PER_UNIT, new Vector2(1, -1) * 20f / PIXELS_PER_UNIT };
                            col.offset = new Vector2(x, y) * 20f / PIXELS_PER_UNIT + Vector2.one * 10 / PIXELS_PER_UNIT;
                            col.usedByComposite = true;
                        }
                        else if (slope == Room.SlopeDirection.DownRight)
                        {
                            var col = obj.AddComponent<PolygonCollider2D>();

                            col.points = new Vector2[3] { new Vector2(-1, 1) * 20f / PIXELS_PER_UNIT, new Vector2(-1, -1) * 20f / PIXELS_PER_UNIT, new Vector2(1, 1) * 20f / PIXELS_PER_UNIT };
                            col.offset = new Vector2(x, y) * 20f / PIXELS_PER_UNIT + Vector2.one * 10 / PIXELS_PER_UNIT;
                            col.usedByComposite = true;
                        }
                        else if (slope == Room.SlopeDirection.DownLeft)
                        {
                            var col = obj.AddComponent<PolygonCollider2D>();

                            col.points = new Vector2[3] { new Vector2(1, 1) * 20f / PIXELS_PER_UNIT, new Vector2(-1, 1) * 20f / PIXELS_PER_UNIT, new Vector2(-1, -1) * 20f / PIXELS_PER_UNIT };
                            col.offset = new Vector2(x, y) * 20f / PIXELS_PER_UNIT + Vector2.one * 10 / PIXELS_PER_UNIT;
                            col.usedByComposite = true;
                        }




                    }
                }
            }

            // Merge them
            compositeCollider.GenerateGeometry();

            // Get rid of the original colliders
            foreach(var col in obj.GetComponents<BoxCollider2D>())
            {
                UnityEngine.Object.Destroy(col);
            }
            foreach (var col in obj.GetComponents<PolygonCollider2D>())
            {
                UnityEngine.Object.Destroy(col);
            }


        }

        private void Update()
        {
            this.water_level = _room.floatWaterLevel;
            #region UnityObjUpdate
            foreach (var pair in _linkedObjects)
            {
                if(pair.Key.slatedForDeletetion || pair.Key.room != _room)
                {
                    UnityEngine.Object.Destroy(pair.Value);
                    _linkedObjects.Remove(pair.Key);
                }
            }
            
            var oldGrav = Physics2D.gravity;
            Physics2D.gravity = new Vector2(0f, -80f);
            _physics.Simulate(1f / 40f);
            Physics2D.gravity = oldGrav;
            #endregion

            
        }

        private void LateUpdate()
        {
            WaterFloatrb();
            CheckBodyChunkAgainstrb();
          
        }


        private void WaterFloatrb()
        {
            
            foreach (var item in _linkedObjects.ToList())
            {
                if (item.Value.GetComponent<Rigidbody2D>().position.y<WATER_LEVEL)
                {
                    Vector2 float_ = Vector2.up * (WATER_LEVEL*4-item.Value.GetComponent<Rigidbody2D>().position.y ) ;
                    Debug.Log(float_);
                    item.Value.GetComponent<Rigidbody2D>().AddForce(float_);
                }
            }
        }


        private void CheckBodyChunkAgainstrb()
        {

            foreach (var obj in _room.updateList)
            {
                if (obj is PhysicalObject Pobj && Pobj.bodyChunks != null && !_linkedObjects.ContainsKey(obj))
                {
                    foreach (BodyChunk b in Pobj.bodyChunks.ToList())
                    {

                        foreach (var item in _linkedObjects.ToList())
                        {

                            ContactFilter2D CF = new ContactFilter2D();
                            CF.useLayerMask = true;
                            CF.layerMask = ~(1 << 2);
                            Collider2D chosen = new Collider2D();
                            Collider2D[] result = new Collider2D[5];
                            int NumberOfresult = _physics.OverlapCircle((b.pos ) / PIXELS_PER_UNIT, (b.rad+b.TerrainRad) / PIXELS_PER_UNIT, CF, result);


                            

                            if (result != null)
                            {

                                // b.contactPoint.y = -1;

                               RaycastHit2D rayresult = _physics.Raycast(b.pos / PIXELS_PER_UNIT, b.vel.normalized, (b.vel.magnitude + b.rad) / PIXELS_PER_UNIT, ~(1 << 2));

                                if (NumberOfresult>0)
                                {
                                    //rayresult.rigidbody.AddForceAtPosition(b.vel/PIXELS_PER_UNIT/40, rayresult.point);
                                    //  b.pos +=(rayresult.point*PIXELS_PER_UNIT-b.vel.normalized*b.rad);

                                    if (NumberOfresult > 1)
                                    {
                                        int index = 0;
                                        float min = 100000;
                                        for (int i = 0; i < NumberOfresult-1; i++)
                                        {
                                            if (Math.Abs((result[i].attachedRigidbody.position * PIXELS_PER_UNIT - b.pos).magnitude) < min)
                                            {
                                                min = Math.Abs((result[i].attachedRigidbody.position * PIXELS_PER_UNIT - b.pos).magnitude);
                                                index = i;
                                            }
                                        }
                                        chosen = result[index];
                                    }else
                                    {
                                        chosen = result[0];
                                    }


                                    if (item.Key is Crate)
                                    {
                                       // bool within = IsPointInRb(chosen.gameObject, b.pos + b.vel);

                                        Vector2[] hitPoint = CloestPointToRb(chosen.gameObject, b.pos, b.vel, b.rad,b.TerrainRad );

                                        //b.vel.x *= b.owner.surfaceFriction;
                                        //b.vel.y *= 0;
                                        //b.vel += result.attachedRigidbody.velocity / 40 * PIXELS_PER_UNIT;
                                        if (PointInRb(chosen.gameObject, b.pos+b.vel.normalized*b.rad+b.vel))
                                        {
                                            b.vel.x *= b.owner.surfaceFriction;
                                            b.vel.y *= 0;
                                            //b.vel.y *= b.owner.bounce * -1;
                                        }
                                        // b.contactPoint.y = -1;
                                        //if (within)
                                        //{
                                           // b.pos = b.pos + (hitPoint[0] - hitPoint[1]);
                                            b.HardSetPosition(b.pos + (hitPoint[0] - hitPoint[1]));
                                            //  item.Value.GetComponent<Rigidbody2D>().AddForceAtPosition(-(hitPoint - b.pos) *(1/ PIXELS_PER_UNIT) *(1/ 40), hitPoint * PIXELS_PER_UNIT);

                                        //}
                                        //else
                                        //{
                                           // b.pos = b.pos + (b.pos  - hitPoint);

                                            //  item.Value.GetComponent<Rigidbody2D>().AddForceAtPosition(-(b.pos - hitPoint) * (1 / PIXELS_PER_UNIT) * (1 / 40), hitPoint * PIXELS_PER_UNIT);
                                        //}



                                        Crate c = item.Key as Crate;
                                        //c.Collide(b.owner, 0, b.index);

                                        (item.Key as Crate).debugSpr.NumberOfPoint[0] = hitPoint[0];
                                        (item.Key as Crate).debugSpr.NumberOfPoint[1] = hitPoint[1];

                                    }

                                    //} else if(NumberOfresult>1)
                                    //{
                                    //    b.pos = b.lastPos;
                                    //}
                                }

                            }
                        }
                    }
                }


            }
        }
        public Dictionary<UpdatableAndDeletable,GameObject> ObjList { get { return this._linkedObjects; } }

        public bool IsPointInRb(GameObject obj, Vector2 p)
        {
           p= obj.transform.InverseTransformPoint(p / PIXELS_PER_UNIT);
            float width = obj.GetComponent<BoxCollider2D>().size.x / 2;
            float height = obj.GetComponent<BoxCollider2D>().size.y / 2;
            if (Math.Abs(p.x) < width || Math.Abs(p.y) < height)
            {
                return true;
            }
            return false;
        }
        public bool PointInRb(GameObject obj, Vector2 p)
        {
            p = obj.transform.InverseTransformPoint(p / PIXELS_PER_UNIT);
            float width = obj.GetComponent<BoxCollider2D>().size.x / 2;
            float height = obj.GetComponent<BoxCollider2D>().size.y / 2;
            if (Math.Abs(p.x) < width && Math.Abs(p.y) < height)
            {
                return true;
            }
            return false;
        }


        public bool IsPointInAnyRb(Vector2 p)
        {        
            foreach (var item in this._linkedObjects) 
            {
                Vector2 point = item.Value.transform.InverseTransformPoint(p / PIXELS_PER_UNIT);
                float width = item.Value.GetComponent<BoxCollider2D>().size.x / 2;
                float height = item.Value.GetComponent<BoxCollider2D>().size.y / 2;
                if (Math.Abs(point.x) < width && Math.Abs(point.y) < height)
                {
                    return true;
                }
            }
            return false;
        }

        public Vector2[] CloestPointToRb(GameObject obj,Vector2 p,Vector2 pVel,float rad,float terrainRad)
        {
           
            Vector2 relativePoint = obj.transform.InverseTransformPoint((p) / PIXELS_PER_UNIT);
            terrainRad /= PIXELS_PER_UNIT;
             rad /= PIXELS_PER_UNIT;
            Vector2[] hitPoint=new Vector2[2];
            
            float width = obj.GetComponent<BoxCollider2D>() .size.x / 2;
            float height = obj.GetComponent<BoxCollider2D>().size.y / 2;
            if (Math.Abs(relativePoint.x) < width || Math.Abs(relativePoint.y) < height)
            {
               
                if (width-Math.Abs(relativePoint.x)  < height -Math.Abs(relativePoint.y) )
                {
                    hitPoint[0] = new Vector2(width * Math.Sign(relativePoint.x) , relativePoint.y);
                    hitPoint[1] = new Vector2(relativePoint.x - (rad+terrainRad) * Math.Sign(relativePoint.x), relativePoint.y);
                }
                else
                {

                    hitPoint[0] = new Vector2(relativePoint.x, height * Math.Sign(relativePoint.y));

                    hitPoint[1] = new Vector2(relativePoint.x, relativePoint.y - (rad+terrainRad)  * Math.Sign(relativePoint.y));
                }
            }
            else
            {
                hitPoint[0] = new Vector2(width * Math.Sign(relativePoint.x), height * Math.Sign(relativePoint.y));
                hitPoint[1] = relativePoint + pVel.normalized * rad;

            }
            hitPoint[0] = obj.transform.TransformPoint(hitPoint[0]) * PIXELS_PER_UNIT;
            hitPoint[1] = obj.transform.TransformPoint(hitPoint[1]) * PIXELS_PER_UNIT;
            return hitPoint;
        }

       

        public  Collider2D IsChunkTouchingGameObject(GameObject obj,Vector2 p, float rad)
        {
            ContactFilter2D CF = new ContactFilter2D();
            CF.useLayerMask = true;
            CF.layerMask = ~(1 << 2);

            return _physics.OverlapCircle(p / PIXELS_PER_UNIT, rad / PIXELS_PER_UNIT,CF);
        }
        private void Dispose()
        {
            SceneManager.UnloadSceneAsync(_scene);
        }
    }
}

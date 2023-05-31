using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TestMod
{
    public class RoomPhysics
    {
        public const float PIXELS_PER_UNIT = 20f;

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
            obj.layer = (1<<2);
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
                    if (tile != null && tile.Solid)
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
                }
            }

            // Merge them
            compositeCollider.GenerateGeometry();

            // Get rid of the original colliders
            foreach(var col in obj.GetComponents<BoxCollider2D>())
            {
                UnityEngine.Object.Destroy(col);
            }


        }

        private void Update()
        {
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
            CheckBodyChunkAgainstrb();
        }
        private void CheckBodyChunkAgainstrb()
        {
            
                foreach (var obj in _room.updateList)
                {
                    if (obj is PhysicalObject Pobj && Pobj.bodyChunks != null&& !_linkedObjects.ContainsKey(obj))
                    {
                        foreach(BodyChunk b in Pobj.bodyChunks.ToList())
                        { 
                           
                            foreach(var item in _linkedObjects.ToList())
                            {  
                                
                                ContactFilter2D CF = new ContactFilter2D();
                                CF.useLayerMask = true;
                                CF.layerMask = ~(1 << 2);
                                Collider2D result= _physics.OverlapCircle((b.pos + b.vel) /PIXELS_PER_UNIT, b.rad/PIXELS_PER_UNIT ,CF);
                                if (result != null)
                                {
                                  


                                    RaycastHit2D rayresult= _physics.Raycast(b.pos / PIXELS_PER_UNIT, b.vel.normalized,(b.vel.magnitude+b.rad)/PIXELS_PER_UNIT,~(1<<2));
                                   
                                    if (rayresult.rigidbody != null)
                                    {
                                      //rayresult.rigidbody.AddForceAtPosition(b.vel/PIXELS_PER_UNIT/40, rayresult.point);
                                    //  b.pos +=(rayresult.point*PIXELS_PER_UNIT-b.vel.normalized*b.rad);
                                    
                                    }

                                if (item.Key is Crate)
                                {
                                    bool within = IsPointInRb(item.Value, b.pos + b.vel);
                                    //Vector2 relativePoint = result.transform.InverseTransformPoint((b.pos + b.vel) / PIXELS_PER_UNIT);
                                    //float rad = (b.rad) / PIXELS_PER_UNIT;
                                    Vector2 hitPoint = CloestPointToRb(item.Value , b.pos, b.vel, b.rad);
                                    //float width = (result as BoxCollider2D).size.x / 2;
                                    //float height = (result as BoxCollider2D).size.y / 2;
                                    //if (Math.Abs(relativePoint.x) < width || Math.Abs(relativePoint.y) < height)
                                    //{
                                    //    within = true;
                                    //    if (Math.Abs(relativePoint.x)-width < Math.Abs(relativePoint.y)-height)
                                    //    {
                                    //        hitPoint = new Vector2(relativePoint.x, height * Math.Sign(relativePoint.y) + rad * Math.Sign(relativePoint.y));
                                    //    }
                                    //    else
                                    //    {

                                    //        hitPoint = new Vector2(width * Math.Sign(relativePoint.x) + rad * Math.Sign(relativePoint.x), relativePoint.y);
                                    //    }
                                    //}
                                    //else
                                    //{
                                    //    hitPoint = new Vector2(width * Math.Sign(relativePoint.x), height * Math.Sign(relativePoint.y));
                                    //}


                                    //hitPoint = result.transform.TransformPoint(hitPoint) * PIXELS_PER_UNIT;

                                    b.vel = result.attachedRigidbody.velocity /40*PIXELS_PER_UNIT;

                                    if (within)
                                    {
                                        b.HardSetPosition(b.pos + (hitPoint - b.pos));
                                       // item.Value.GetComponent<Rigidbody2D>().position -= (hitPoint - b.pos) / 2 / PIXELS_PER_UNIT;
                                        item.Value.GetComponent<Rigidbody2D>().AddForceAtPosition(-(hitPoint - b.pos) *(1/ PIXELS_PER_UNIT) *(1/ 40), hitPoint / PIXELS_PER_UNIT);

                                    }
                                    else
                                    {
                                        b.HardSetPosition(b.pos + (b.pos - hitPoint));
                                       // item.Value.GetComponent<Rigidbody2D>().position -= (b.pos - hitPoint) / 2 / PIXELS_PER_UNIT;
                                        item.Value.GetComponent<Rigidbody2D>().AddForceAtPosition(-(b.pos - hitPoint) * (1 / PIXELS_PER_UNIT) * (1 / 40), hitPoint / PIXELS_PER_UNIT);
                                    }

                                   

                                    Crate c = item.Key as Crate;
                                    //c.Collide(b.owner, 0, b.index);

                                   // (item.Key as Crate).debugSpr.NumberOfPoint[0] =hitPoint;

                                }


                            }
                               
                            }
                        }
                    }
                }
            
            
        }
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
        public Vector2 CloestPointToRb(GameObject obj,Vector2 p,Vector2 pVel,float rad)
        {
            Vector2 relativePoint = obj.transform.InverseTransformPoint((p + pVel) / PIXELS_PER_UNIT);
             rad /= PIXELS_PER_UNIT;
            Vector2 hitPoint;
            float width = obj.GetComponent<BoxCollider2D>() .size.x / 2;
            float height = obj.GetComponent<BoxCollider2D>().size.y / 2;
            if (Math.Abs(relativePoint.x) < width || Math.Abs(relativePoint.y) < height)
            {
               
                if (Math.Abs(relativePoint.x) - width < Math.Abs(relativePoint.y) - height)
                {
                    hitPoint = new Vector2(relativePoint.x, height * Math.Sign(relativePoint.y) + rad * Math.Sign(relativePoint.y));
                }
                else
                {

                    hitPoint = new Vector2(width * Math.Sign(relativePoint.x) + rad * Math.Sign(relativePoint.x), relativePoint.y);
                }
            }
            else
            {
                hitPoint = new Vector2(width * Math.Sign(relativePoint.x), height * Math.Sign(relativePoint.y));
            }
            hitPoint = obj.transform.TransformPoint(hitPoint) * PIXELS_PER_UNIT;
            return hitPoint;
        }
        private void Dispose()
        {
            SceneManager.UnloadSceneAsync(_scene);
        }
    }
}

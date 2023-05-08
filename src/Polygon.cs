using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Drawing.Drawing2D;

namespace TestMod
{
    class Polygon
    {
        public UnityEngine.Vector2 center;
        public UnityEngine.Vector2[] corners;
        public UnityEngine.Vector2[] cornersLastPos;
        public UnityEngine.Vector2[] cornersLastLastPos;
        private List<Vector2> edges = new List<Vector2>();
        public float width;
        public float height;
        public float angleDeg;
        public List<TilePolygon> collisionContainer; 
        private Vector2[] originalCorners;

        public Polygon(Vector2 center, float width, float height, Vector2[] origCorners,float ang=0)
        {
            this.center = center;
            this.width = width;
            this.height = height;

           // for(int i = 0; i < origCorners.Length; i++) { origCorners[i] *= new Vector2(width, height); }
            originalCorners = origCorners;
            corners = origCorners;
            cornersLastPos= origCorners;
            cornersLastLastPos= origCorners;

            edges = new List<Vector2>();
            angleDeg = ang;
            UpdateCornerPoints();
           
            Debug.Log("Adding actual polygon list!");
            collisionContainer = new List<TilePolygon>();
            
        }

        public Polygon(Vector2 center, int NumPoint,float scale, float ang = 0)
        {
            if (NumPoint < 3) NumPoint = 3;
            angleDeg = ang;
            this.center=center;
            this.width = scale;
            this.height = scale;
            this.originalCorners = new Vector2[NumPoint];
            for(int i = 0; i < NumPoint; i++) { this.originalCorners[i]= RWCustom.Custom.RotateAroundOrigo(Vector2.up*scale,(((360/NumPoint)+angleDeg)*i)%360);}
          //  for (int i = 0; i < NumPoint; i++) { originalCorners[i] *= new Vector2(scale,scale); }




            this.corners = originalCorners;
            this.cornersLastPos = this.originalCorners;
            this.cornersLastLastPos = this.cornersLastPos;

            
            collisionContainer = new List<TilePolygon>();
            UpdateCornerPoints();
        }

        public void Move(UnityEngine.Vector2 velocity)
        {
            center += velocity * Time.deltaTime;
        }
        public void UpdateScale(float scale)
        {
            this.width = scale;
            this.height = scale;
        }

        public void UpdateScale(float Width, float Height)
        {
            this.width = Width;
            this.height= Height;
        }


        public void UpdateCornerPoints()
        {
            //Debug.Log(center);
            // Define the corner points of the shape

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = originalCorners[i];
                corners[i]*=new Vector2(width, height);
            }

            // Define edges
            BuildEdges();

            // Loop through each corner point
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = RWCustom.Custom.RotateAroundOrigo(corners[i], 45f);
                corners[i] += center;
            }
        }

        public void UpdateCornerPointsWithAngle(float angleAdded)
        {
            // Define the corner points of the shape

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = originalCorners[i];
                corners[i] *= new Vector2(width, height);
            }

            // Define Edges
            BuildEdges();

            // Loop through each corner point
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = RWCustom.Custom.RotateAroundOrigo(corners[i], 45f);
                corners[i] += center;
            }

        }

        public void BuildEdges()
        {
            Vector2 p1;
            Vector2 p2;
            Edges.Clear();
            for (int i = 0; i < corners.Length; i++)
            {
                p1 = new Vector2(corners[i].x, corners[i].y);
                if (i + 1 >= corners.Length)
                {
                    p2 = new Vector2(corners[0].x, corners[0].y);
                }
                else
                {
                    p2 = new Vector2(corners[i + 1].x, corners[i + 1].y);
                }
                edges.Add(p2 - p1);
            }
        }

        public List<Vector2> Edges
        {
            get { return edges; }
        }
    }
}

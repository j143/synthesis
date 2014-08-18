﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inventor;
using System.Threading;
using System.Runtime.Caching;

/// <summary>
/// Handles a single thread to find the radius of a single component.
/// </summary>
class FindRadiusThread
{
    Thread findRadius;
    ComponentOccurrence component; //The component of which to find the radius.
    BXDVector3 rotationAxis; //The vector that is being rotated around in terms of the part's axes.
    private static double currentMaxRadius; //The largest radius found among all the parts in the rigid group.
    static ComponentOccurrence treadPart; //The component with the largest radius.  This is stored so its width can be found later.
    private double finalLocalMaxRadius = 0.0;
    public volatile bool endThread = false;


    public FindRadiusThread(ComponentOccurrence passComponent, BXDVector3 passRotationAxis)
    {
        findRadius = new Thread(() => FindMaxRadius());
        component = passComponent;
        rotationAxis = passRotationAxis;
    }

    /// <summary>
    /// Sets the largest radius found back to zero.  This needs to be done between rigid groups.
    /// </summary>
    static public void Reset()
    {
        currentMaxRadius = 0;
        treadPart = null;
    }

    static public double GetRadius()
    {
        return currentMaxRadius;
    }

    public double GetLocalRadius()
    {
        return finalLocalMaxRadius;    
    }

    static public ComponentOccurrence GetWidthComponent()
    {
        return treadPart;
    }

    public void Start()
    {

        findRadius.Start();
    }

    public void Join()
    {
        findRadius.Join();
    }

    public bool GetIsAlive()
    {
        return findRadius.IsAlive;
    }

    /// <summary>
    /// Calculates the largest radius and its component.
    /// </summary>
    public void FindMaxRadius()
    {
        Vector myRotationAxis = Program.INVENTOR_APPLICATION.TransientGeometry.CreateVector(); //The axis of rotation relative to the part's axes.
        Matrix asmToPart = Program.INVENTOR_APPLICATION.TransientGeometry.CreateMatrix(); //The transformation from assembly axes to part axes.
        Matrix transformedVector = Program.INVENTOR_APPLICATION.TransientGeometry.CreateMatrix(); //Stores the axis of rotation in matrix form.
        Vector vertexVector;
        Inventor.Point origin;
        Vector partXAxis;
        Vector partYAxis;
        Vector partZAxis;
        Vector asmXAxis = Program.INVENTOR_APPLICATION.TransientGeometry.CreateVector(1, 0, 0);
        Vector asmYAxis = Program.INVENTOR_APPLICATION.TransientGeometry.CreateVector(0, 1, 0);
        Vector asmZAxis = Program.INVENTOR_APPLICATION.TransientGeometry.CreateVector(0, 0, 1);

        component.Transformation.GetCoordinateSystem(out origin, out partXAxis, out partYAxis, out partZAxis);

        asmToPart.SetToAlignCoordinateSystems(origin, partXAxis, partYAxis, partZAxis, origin, asmXAxis, asmYAxis, asmZAxis);

        transformedVector.Cell[1, 1] = rotationAxis.x;
        transformedVector.Cell[2, 1] = rotationAxis.y;
        transformedVector.Cell[3, 1] = rotationAxis.z;
        
        //Changes the rotation axis from being expressed by assembly axes to occurrence axes.
        transformedVector.TransformBy(asmToPart);


        myRotationAxis.X = transformedVector.Cell[1, 1];
        myRotationAxis.Y = transformedVector.Cell[2, 1];
        myRotationAxis.Z = transformedVector.Cell[3, 1];

        double localMaxRadius = 0.0;

        foreach (SurfaceBody surface in component.Definition.SurfaceBodies)
        {
            foreach (Vertex vertex in surface.Vertices)
            {
                //Grabs the three doubles that make up a coordinate for a single vertex.
                vertexVector = Program.INVENTOR_APPLICATION.TransientGeometry.CreateVector(vertex.Point.X, vertex.Point.Y, vertex.Point.Z);

                //Crossproduct returns a vector with the magnitude of the distance between the two orthagonal to myRotationAxis.
                //Direction doesn't matter, onlyh the magnitude.

                double localRadius = myRotationAxis.CrossProduct(vertexVector).Length;  

                if (endThread)
                {
                    return;
                }

                if (localRadius > localMaxRadius)
                {
                    localMaxRadius = localRadius;
                }
            }
        }

        //Stores the largest radius in shared memory once the largest radius for this component is calculated.
        lock (Program.INVENTOR_APPLICATION)
        {
            if (localMaxRadius > currentMaxRadius)
            {
                currentMaxRadius = localMaxRadius;

                treadPart = component;
            }
        }

        Console.WriteLine("Found radius of " + component.Name + " to be " + localMaxRadius);
    }   
}


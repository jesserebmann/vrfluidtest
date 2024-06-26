﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;

namespace Obi
{

    [AddComponentMenu("Physics/Obi/Emitter shapes/Disk", 851)]
	[ExecuteInEditMode]
	public class ObiEmitterShapeDisk : ObiEmitterShape
	{
		public float radius = 0.5f;
		public bool edgeEmission = false;

		public void OnValidate(){
			radius = Mathf.Max(0,radius);
		}

		public override void GenerateDistribution(){

			distribution.Clear(); 

			if (edgeEmission)
			{
				if (particleSize > 0)
				{
					// len = 2 sin(angle/2) * radius
					float angleIncrement = Mathf.Asin(particleSize / (2 * radius)) * 2;
					float steps = 2 * Mathf.PI / angleIncrement;

					for (int j = 0; j < steps; ++j){
						Vector3 pos = new Vector3(radius * Mathf.Cos(angleIncrement*j), radius * Mathf.Sin(angleIncrement*j),0);
						distribution.Add(new EmitPoint(pos,pos.normalized));
					}
				}
			}
			else
			{
				if (particleSize > 0)
				{
					int numCircles = (int)(radius / particleSize);

					for (int i = 0; i <= numCircles; ++i){

						if (i == 0){
							distribution.Add(new EmitPoint(Vector3.zero,Vector3.forward));
							continue;
						}
					
						float r = particleSize * i;

						// len = 2 sin(angle/2) * radius
						float angleIncrement = Mathf.Asin(particleSize / (2 * r)) * 2;
						float steps = 2 * Mathf.PI / angleIncrement;

						for (int j = 0; j < steps; ++j){
							Vector3 pos = new Vector3(r * Mathf.Cos(angleIncrement*j), r * Mathf.Sin(angleIncrement*j),0);
							distribution.Add(new EmitPoint(pos,Vector3.forward));
						}
					}
				}
			}
		}

	#if UNITY_EDITOR
		public void OnDrawGizmosSelected(){

			Handles.matrix = transform.localToWorldMatrix;
			Handles.color  = Color.cyan;

			Handles.DrawWireDisc(Vector3.zero,Vector3.forward,radius);

			foreach (EmitPoint point in distribution)
				Handles.ArrowHandleCap(0,point.position,Quaternion.LookRotation(point.direction),0.05f * point.direction.magnitude,EventType.Repaint);

		}
	#endif

	}
}


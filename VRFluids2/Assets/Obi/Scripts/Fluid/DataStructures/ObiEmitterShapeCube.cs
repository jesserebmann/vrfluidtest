﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Emitter shapes/Cube", 870)]
	[ExecuteInEditMode]
	public class ObiEmitterShapeCube : ObiEmitterShape
	{
		public enum SamplingMethod{
			SURFACE,		/**< distributes particles in the surface of the object.*/
			VOLUME			/**< distributes particles in the volume of the object.*/
		}

		public SamplingMethod samplingMethod = SamplingMethod.VOLUME;

		public Vector3 size = Vector3.one * 0.25f;

		public void OnValidate(){
			size.x = Mathf.Max(0,size.x);
			size.y = Mathf.Max(0,size.y);
			size.z = Mathf.Max(0,size.z);
		}

		public override void GenerateDistribution(){

			distribution.Clear(); 

			if (particleSize <= 0) return;

			switch (samplingMethod)
			{
				case SamplingMethod.VOLUME:
				{
	
					int numX = Mathf.CeilToInt(size.x/particleSize);
					int numY = Mathf.CeilToInt(size.y/particleSize);
					int numZ = Mathf.CeilToInt(size.z/particleSize);

					for (int x = 0; x <= numX; ++x){
						for (int y = 0; y <= numY; ++y){
							for (int z = 0; z <= numZ; ++z){
								Vector3 pos = new Vector3(x * size.x/(float)numX - size.x*0.5f,
														  y * size.y/(float)numY - size.y*0.5f,
														  z * size.z/(float)numZ - size.z*0.5f);
								distribution.Add(new EmitPoint(pos,Vector3.forward));
							}
						}
					}
	
				}break;

				case SamplingMethod.SURFACE:
				{

					int numX = Mathf.CeilToInt(size.x/particleSize);
					int numY = Mathf.CeilToInt(size.y/particleSize);
					int numZ = Mathf.CeilToInt(size.z/particleSize);

					for (int x = 0; x <= numX; ++x){
						for (int y = 0; y <= numY; ++y){
							for (int z = 0; z <= numZ; ++z){

								if (x == 0 || x == numX ||
									y == 0 || y == numY ||
									z == 0 || z == numZ ){

									Vector3 pos = new Vector3(x * size.x/(float)numX - size.x*0.5f,
															  y * size.y/(float)numY - size.y*0.5f,
															  z * size.z/(float)numZ - size.z*0.5f);
			
									Vector3 normal = Vector3.zero;

									if (x == 0) normal.x = -1;
									else if (x == numX) normal.x = 1;

									if (y == 0) normal.y = -1;
									else if (y == numY) normal.y = 1;

									if (z == 0) normal.z = -1;
									else if (z == numZ) normal.z = 1;

									distribution.Add(new EmitPoint(pos,normal.normalized));
								}
							}
						}
					}
					
				}break;
			}
		}

	#if UNITY_EDITOR
		public void OnDrawGizmosSelected(){

			Handles.matrix = transform.localToWorldMatrix;
			Handles.color  = Color.cyan;

			Handles.DrawWireCube(Vector3.zero,size);

			foreach (EmitPoint point in distribution)
				Handles.ArrowHandleCap(0,point.position,Quaternion.LookRotation(point.direction),0.05f,EventType.Repaint);

		}
	#endif

	}
}


﻿using UnityEngine;

namespace Puppeteer
{
	public static class MatrixTools
	{
		public static Vector3 TranslationFrom(ref Matrix4x4 matrix)
		{
			Vector3 translate;
			translate.x = matrix.m03;
			translate.y = matrix.m13;
			translate.z = matrix.m23;
			return translate;
		}

		public static Quaternion RotationFrom(ref Matrix4x4 matrix)
		{
			Vector3 forward;
			forward.x = matrix.m02;
			forward.y = matrix.m12;
			forward.z = matrix.m22;

			Vector3 upwards;
			upwards.x = matrix.m01;
			upwards.y = matrix.m11;
			upwards.z = matrix.m21;

			if (forward == Vector3.zero || upwards == Vector3.zero)
				return Quaternion.identity;
			return Quaternion.LookRotation(forward, upwards);
		}

		public static Vector3 ScaleFrom(ref Matrix4x4 matrix)
		{
			Vector3 scale;
			scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
			scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
			scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
			return scale;
		}

		public static Matrix4x4 Offset(this Matrix4x4 matrix, Vector3 delta)
		{
			return Matrix4x4.TRS(
				TranslationFrom(ref matrix) + delta,
				RotationFrom(ref matrix),
				ScaleFrom(ref matrix)
			);
		}

		public static Matrix4x4 OffsetRef(this ref Matrix4x4 matrix, Vector3 delta)
		{
			return Matrix4x4.TRS(
				TranslationFrom(ref matrix) + delta,
				RotationFrom(ref matrix),
				ScaleFrom(ref matrix)
			);
		}
	}
}

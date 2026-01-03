using System.Collections.Generic;
using UnityEngine;

namespace VRM2Magica.Runtime
{
    /// <summary>
    /// 与えられたTransformの一覧から、胸ボーンに相当するTransformのペアを返却する
    /// </summary>
    public static class BreastUtil
    {
        public static (Transform Left, Transform Right)? SelectBestPair(
            List<Transform> candidates,
            Transform root,
            float minXSeparation = 0.03f
        )
        {
            if (candidates.Count < 2)
            {
                return null;
            }

            (Transform t1, Transform t2)? bestPair = null;
            float bestScore = float.MinValue;

            // すべての組み合わせを総当たりで検証
            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var t1 = candidates[i];
                    var t2 = candidates[j];

                    var p1 = root.InverseTransformPoint(t1.position);
                    var p2 = root.InverseTransformPoint(t2.position);

                    // Z軸が背中側に位置するものは弾く
                    if (p1.z < 0 || p2.z < 0)
                    {
                        continue;
                    }

                    // X軸が左右逆の位置にあるか
                    if (Mathf.Approximately(Mathf.Sign(p1.x), Mathf.Sign(p2.x)))
                    {
                        continue;
                    }

                    // X座標が中心に近すぎるものを弾く
                    if (Mathf.Abs(p1.x) < minXSeparation || Mathf.Abs(p2.x) < minXSeparation)
                    {
                        continue;
                    }

                    // 対称性のスコアを出す
                    // 鏡像位置とのズレが小さいほど高評価
                    // p1を反転させた座標とp2の距離を測る
                    var p1Mirrored = new Vector3(-p1.x, p1.y, p1.z);
                    var symmetryError = Vector3.Distance(p1Mirrored, p2);

                    // 対称性が著しく低いものを弾く
                    if (symmetryError > 0.1f)
                    {
                        continue;
                    }

                    // 高さの一致度のスコア
                    var heightDiff = Mathf.Abs(p1.y - p2.y);

                    // 前方への突き抜け度のスコア
                    // 突き出ているほど胸である可能性が高いと判断し、スコアを加算する
                    var forwardness = (p1.z + p2.z) * 0.5f;

                    // 対称誤差が小さい+高さズレが小さい+前にある
                    var score = forwardness - (symmetryError * 10.0f) - (heightDiff * 10.0f);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPair = (t1, t2);
                    }
                }
            }

            if (bestPair.HasValue)
            {
                var p1 = root.InverseTransformPoint(bestPair.Value.t1.position);
                return p1.x > 0 ? (bestPair.Value.t1, bestPair.Value.t2) : (bestPair.Value.t2, bestPair.Value.t1);
            }

            return null;
        }
    }
}
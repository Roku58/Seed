using Unity.Cinemachine;
using UnityEngine;

namespace Seed.Cameras
{
    /// <summary>
    /// よく使うカメラ構成の組み立て補助（一人称・三人称・固定）。
    ///
    /// Cinemachine のカメラは「本体（CinemachineCamera）＋位置の作り方＋向きの作り方」の
    /// 部品構成で決まる。その定番の組み合わせをコード1行で作れるようにしたもの——
    /// プレハブを用意する運用でも構わないが、アセットを持たない段階から
    /// 視点切替を動かせるようにしておくため（デモ・プロトタイプ向け）。
    ///
    /// 「どんな距離・画角にするか」はゲームの絵作りの方針なので、既定値は控えめにし、
    /// 呼び出し側（App 層）が引数で決める形にしている。
    /// </summary>
    public static class CameraRigBuilder
    {
        /// <summary>
        /// カメラに Cinemachine ブレインを用意する（既にあればそれを返す）。
        /// ブレインは「有効なバーチャルカメラの絵を実カメラへ焼き付ける」係で、
        /// 視点の合成（ブレンド）を担当する。
        /// </summary>
        public static CinemachineBrain EnsureBrain(UnityEngine.Camera camera,
            float defaultBlendSeconds = 0.5f)
        {
            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = camera.gameObject.AddComponent<CinemachineBrain>();
            }
            brain.DefaultBlend = defaultBlendSeconds <= 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                : new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, defaultBlendSeconds);
            return brain;
        }

        /// <summary>
        /// 一人称カメラを作る（追従対象の位置と向きに完全一致させる）。
        /// 対象には頭のボーンや目の高さのダミーを渡す——首の動きがそのまま画面になる。
        /// </summary>
        public static CinemachineCamera CreateFirstPerson(string name,
            Transform parent = null, float fieldOfView = 75f)
        {
            var camera = CreateCamera(name, parent, fieldOfView);
            camera.gameObject.AddComponent<CinemachineHardLockToTarget>();
            camera.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();
            return camera;
        }

        /// <summary>
        /// 三人称カメラを作る（肩越しの追従。壁との衝突回避つき）。
        /// distance は対象からの距離、shoulderSide は右肩(+)/左肩(-)、height は腰から目の高さ。
        /// </summary>
        public static CinemachineCamera CreateThirdPerson(string name,
            Transform parent = null, float distance = 4f, float shoulderSide = 0.4f,
            float height = 1.4f, float fieldOfView = 60f)
        {
            var camera = CreateCamera(name, parent, fieldOfView);
            var follow = camera.gameObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(shoulderSide, height, 0f);
            follow.CameraDistance = distance;
            follow.Damping = new Vector3(0.1f, 0.5f, 0.3f); // 横は素早く・縦はぬるく
            follow.VerticalArmLength = 0.2f;
            follow.CameraSide = shoulderSide >= 0f ? 1f : 0f;
            return camera;
        }

        /// <summary>
        /// 固定カメラを作る（俯瞰・定点。追従しないので
        /// <see cref="CameraDirector.Register"/> では followsTarget: false で登録する）。
        /// </summary>
        public static CinemachineCamera CreateFixed(string name, Vector3 position,
            Vector3 lookAtPoint, Transform parent = null, float fieldOfView = 60f)
        {
            var camera = CreateCamera(name, parent, fieldOfView);
            camera.transform.position = position;
            var direction = lookAtPoint - position;
            if (direction.sqrMagnitude > 1e-6f)
            {
                camera.transform.rotation = Quaternion.LookRotation(direction.normalized);
            }
            return camera;
        }

        /// <summary>カメラ本体を作る（共通部分）。</summary>
        private static CinemachineCamera CreateCamera(string name, Transform parent,
            float fieldOfView)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            var camera = go.AddComponent<CinemachineCamera>();
            var lens = camera.Lens;
            lens.FieldOfView = fieldOfView;
            camera.Lens = lens;
            camera.Priority = 0; // 有効化は CameraDirector が行う
            return camera;
        }
    }
}

# URPLab Auto Retopology 확장의 AutoRemesher(쿼드 리메시)를 스크립트에서 쓰는 도우미.
# 확장 패널의 Generate Low Poly와 같은 일을 해요: 메시를 npz로 넘기고 확장의 remesh_worker.py를 따로 돌려 결과를 받아요.
# 확장이 설치돼 있어야 해요 (Blender 5.2 > Get Extensions > URPLab Auto Retopology).
import bpy, os, sys, json, subprocess, tempfile
import numpy as np

_EXT = os.path.join(os.environ.get("APPDATA", ""), "Blender Foundation", "Blender",
                    f"{bpy.app.version[0]}.{bpy.app.version[1]}", "extensions")
WORKER = os.path.join(_EXT, "user_default", "urplab_auto_retopology", "remesh_worker.py")
_WHEELS = os.path.join(_EXT, "user_default", "urplab_auto_retopology", "wheels")
# 주의: --factory-startup으로 켜면 Blender가 꺼진 확장의 모듈을 extensions/.local에서 지워요.
# 그래서 확장 폴더의 wheel을 임시 폴더에 따로 풀어 써요 (사용자 설치본은 건드리지 않음)
SITE = os.path.join(tempfile.gettempdir(), "athousanddawns_autoremesher_core")


def _ensure_core():
    if os.path.isfile(os.path.join(SITE, "autoremesher_core.pyd")):
        return
    import glob, zipfile
    wheels = glob.glob(os.path.join(_WHEELS, "autoremesher_core-*.whl"))
    if not wheels:
        raise RuntimeError(f"AutoRemesher wheel을 찾지 못했어요: {_WHEELS}")
    zipfile.ZipFile(wheels[0]).extractall(SITE)


def _python():
    # Blender 안에서 sys.executable은 blender.exe라서, 확장처럼 번들 python.exe를 찾아요
    p = os.path.join(sys.prefix, "bin", "python.exe")
    return p if os.path.isfile(p) else sys.executable


def autoremesh(mesh, target_quads, preserve_thin=True, island_detail=10):
    """mesh(bpy Mesh)를 쿼드 리메시해 새 Mesh로 돌려줘요. 원본은 그대로예요."""
    if not os.path.isfile(WORKER):
        raise RuntimeError(f"URPLab Auto Retopology 확장을 찾지 못했어요: {WORKER}")
    _ensure_core()
    mesh.calc_loop_triangles()
    verts = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get("co", verts)
    tris = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
    mesh.loop_triangles.foreach_get("vertices", tris)
    fd, src = tempfile.mkstemp(suffix=".npz"); os.close(fd)
    fd, dst = tempfile.mkstemp(suffix=".npz"); os.close(fd); os.remove(dst)
    try:
        params = {"target_quads": int(target_quads), "preserve_thin": preserve_thin,
                  "island_detail": int(island_detail), "no_lost_geometry": True}
        np.savez(src, vertices=verts.reshape(-1, 3), triangles=tris.reshape(-1, 3).astype(np.uint32),
                 parameters=json.dumps(params))
        env = os.environ.copy()
        env["PYTHONPATH"] = SITE + os.pathsep + env.get("PYTHONPATH", "")
        proc = subprocess.run([_python(), WORKER, src, dst], env=env, capture_output=True, text=True)
        if proc.returncode != 0 or not os.path.isfile(dst):
            raise RuntimeError("AutoRemesher 실패: " + proc.stdout[-800:] + proc.stderr[-800:])
        with np.load(dst) as data:
            v, idx, sizes = data["vertices"], data["face_indices"], data["face_sizes"]
        faces, o = [], 0
        for n in sizes:
            faces.append(tuple(int(i) for i in idx[o:o + n])); o += n
        out = bpy.data.meshes.new(mesh.name + "_Retopo")
        out.from_pydata(v.tolist(), [], faces)
        out.update(calc_edges=True)
        return out
    finally:
        for p in (src, dst):
            if os.path.exists(p):
                os.remove(p)

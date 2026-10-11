#!/usr/bin/env python
"""PNG 프레임 폴더를 GIF로 조립하는 개발 도구. (Unity 프로젝트의 Assets/ 밖에 둔다)

사용 예:
    python -I tools/make_gif.py Temp/GifFrames docs/images/stage1-flat-gps.gif \
        --fps 10 --width 640 --max-mb 10 --pillow-path <Pillow 임시 설치 폴더>

Pillow는 저장소에 포함하지 않는다. 임시 폴더에 설치해서 --pillow-path로 넘기고, 끝나면 폴더를 지운다.
    pip install Pillow --target <임시폴더>

용량이 --max-mb를 넘으면 프레임 수 -> 해상도 -> 색상 수 순으로 줄여 한도 안에 맞춘다.
"""
import argparse
import glob
import os
import sys


def parse_args():
    p = argparse.ArgumentParser(description="PNG 프레임 폴더를 GIF로 변환한다.")
    p.add_argument("frames_dir", help="frame_0000.png 형태의 PNG가 들어 있는 폴더")
    p.add_argument("out_gif", help="출력 GIF 경로")
    p.add_argument("--fps", type=float, default=10.0, help="입력 프레임의 초당 저장 수 (기본 10)")
    p.add_argument("--width", type=int, default=640, help="시작 가로 해상도 (기본 640, 원본이 작으면 유지)")
    p.add_argument("--max-mb", type=float, default=10.0, help="최대 파일 크기 MB (기본 10)")
    p.add_argument("--pillow-path", default=None, help="Pillow가 설치된 폴더 (pip install --target 위치)")
    return p.parse_args()


def load_pillow(pillow_path):
    if pillow_path:
        sys.path.insert(0, os.path.abspath(pillow_path))
    try:
        from PIL import Image  # noqa: F401
    except ImportError:
        sys.exit("Pillow를 찾을 수 없습니다. 임시 폴더에 설치한 뒤 --pillow-path로 지정하세요:\n"
                 "  pip install Pillow --target <임시폴더>")
    import PIL.Image
    return PIL.Image


def build(frames, width, colors, keep, fps, out_path, Image):
    selected = frames[::keep]
    resized = []
    for im in selected:
        if im.width > width:
            h = round(im.height * width / im.width)
            im = im.resize((width, h), Image.LANCZOS)
        resized.append(im)

    # 모든 프레임이 같은 팔레트를 쓰도록 샘플 프레임을 이어 붙여 전역 팔레트를 만든다.
    step = max(1, len(resized) // 8)
    samples = resized[::step][:8]
    sheet = Image.new("RGB", (samples[0].width, samples[0].height * len(samples)))
    for i, s in enumerate(samples):
        sheet.paste(s, (0, i * samples[0].height))
    palette_img = sheet.quantize(colors=colors, method=Image.MEDIANCUT)

    dither = getattr(getattr(Image, "Dither", Image), "NONE")
    paletted = [im.quantize(palette=palette_img, dither=dither) for im in resized]

    duration_ms = int(round(1000.0 / fps * keep))
    paletted[0].save(out_path, save_all=True, append_images=paletted[1:],
                     duration=duration_ms, loop=0, optimize=False, disposal=1)
    return len(paletted), resized[0].size


def main():
    # 한글 메시지가 콘솔/도구 출력에서 깨지지 않도록 UTF-8로 고정한다.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    args = parse_args()
    Image = load_pillow(args.pillow_path)

    paths = sorted(glob.glob(os.path.join(args.frames_dir, "frame_*.png")))
    if not paths:
        sys.exit(f"프레임을 찾을 수 없습니다: {args.frames_dir}/frame_*.png")

    frames = [Image.open(p).convert("RGB") for p in paths]
    limit = int(args.max_mb * 1024 * 1024)
    os.makedirs(os.path.dirname(os.path.abspath(args.out_gif)), exist_ok=True)

    # (프레임 간격, 가로 해상도, 색상 수) - 품질이 높은 순
    levels = [
        (1, args.width, 256),
        (2, args.width, 256),
        (2, min(args.width, 480), 256),
        (2, min(args.width, 400), 128),
        (3, min(args.width, 320), 128),
        (3, min(args.width, 320), 64),
    ]

    for keep, width, colors in levels:
        count, size = build(frames, width, colors, keep, args.fps, args.out_gif, Image)
        file_size = os.path.getsize(args.out_gif)
        print(f"시도: 프레임 1/{keep}, 가로 {width}px, {colors}색 -> {file_size / 1024 / 1024:.2f}MB")
        if file_size <= limit:
            seconds = count * keep / args.fps
            print(f"완료: {args.out_gif} ({count}프레임, {size[0]}x{size[1]}, 약 {seconds:.1f}초, "
                  f"{file_size / 1024 / 1024:.2f}MB)")
            return

    sys.exit(f"가장 낮은 품질에서도 {args.max_mb}MB를 넘습니다. 녹화 길이를 줄여 보세요.")


if __name__ == "__main__":
    main()

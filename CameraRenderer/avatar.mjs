import { createHmac, timingSafeEqual } from 'node:crypto';
import { crc32 } from 'node:zlib';

// The page draws at most this many frames for one moving image.
export const AVATAR_FRAME_LIMIT = 32;
export const AVATAR_ROOM_ID = 2000000001;
// An avatar draws 12 animation frames a second.
const FRAMES_PER_SECOND = 12;
// A loop counts only when this many later frames repeat it.
const LOOP_PROOF_FRAMES = 4;
const PNG_SIGNATURE = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
const AVATAR_PATH = /^\/avatars\/v([1-9]\d{0,5})\/([a-z0-9][a-z0-9.-]{0,239})\/([a-f0-9]{16})\.([a-z0-9-]{1,80})\.png$/;
const VARIANT = /^(m|f)-(s|m|l)-(full|head)-([0-7])-([0-7])-(std|sit|lay|wlk)-(none|wave|kiss|laugh)-(nrm|sml|sad|agr|srp)-([0-4])-(0|[1-9]\d{0,4})-(0|[1-9]\d{0,4})-(still|anim)$/;
const SCALES = { s: 0.5, m: 1, l: 2 };
const POSTURES = { std: 'std', sit: 'sit', lay: 'lay', wlk: 'mv' };
const EXPRESSIONS = { none: 0, wave: 1, kiss: 2, laugh: 3 };

// The signature covers the figure and the version, so only the hotel's own site can ask for a new figure.
export function avatarSignature(secret, version, figure) {
    return createHmac('sha256', secret).update(`v${version}/${figure}`).digest('hex').slice(0, 16);
}

// Reads /avatars/v<version>/<figure>/<signature>.<variant>.png. Null means the path is not one this hotel signed.
export function avatarRequest(pathname, secret) {
    const path = AVATAR_PATH.exec(pathname);
    if (!path) return null;
    const [, version, figure, signature, variant] = path;
    if (figure.includes('..') || figure.endsWith('.') || figure.endsWith('-')) return null;
    if (!timingSafeEqual(Buffer.from(signature), Buffer.from(avatarSignature(secret, version, figure)))) return null;
    const fields = VARIANT.exec(variant);
    if (!fields) return null;
    const [, gender, size, part, direction, headDirection, posture, expression, gesture, dance, effect, item, motion] = fields;
    const job = {
        figure, gender: gender.toUpperCase(), part, direction: Number(direction), headDirection: Number(headDirection),
        posture: POSTURES[posture], expression: EXPRESSIONS[expression], gesture: gesture === 'nrm' ? '' : gesture,
        dance: Number(dance), effect: Number(effect), handItem: Number(item),
        frames: motion === 'anim' ? AVATAR_FRAME_LIMIT : 1, scale: SCALES[size]
    };
    // A head has one spelling: it looks one way, stands still and does nothing else.
    if (part === 'head' && (direction !== headDirection || posture !== 'std' || job.expression || job.dance || job.effect || job.handItem || motion !== 'still')) return null;
    return { job, file: `v${version}/${figure}/${signature}.${variant}.png` };
}

// The shortest run of frames that the frames after it repeat. Frames that never repeat are one long run.
export function loopLength(frames) {
    for (let length = 1; length <= frames.length - LOOP_PROOF_FRAMES; length++) {
        let repeats = true;
        for (let index = 0; repeats && index + length < frames.length; index++) repeats = frames[index].equals(frames[index + length]);
        if (repeats) return length;
    }
    return frames.length;
}

function pngChunks(image) {
    if (!Buffer.isBuffer(image) || image.length < 57 || !image.subarray(0, 8).equals(PNG_SIGNATURE)) throw new Error('Avatar frame is not a PNG');
    const chunks = [];
    for (let offset = 8; offset + 12 <= image.length;) {
        const length = image.readUInt32BE(offset);
        if (offset + 12 + length > image.length) throw new Error('Avatar frame is cut short');
        chunks.push({ type: image.toString('ascii', offset + 4, offset + 8), data: image.subarray(offset + 8, offset + 8 + length) });
        offset += 12 + length;
    }
    const header = chunks[0];
    const data = chunks.filter(chunk => chunk.type === 'IDAT').map(chunk => chunk.data);
    if (header?.type !== 'IHDR' || header.data.length !== 13 || !data.length) throw new Error('Avatar frame has no image');
    return { header: header.data, data };
}

function pngChunk(type, data) {
    const chunk = Buffer.alloc(12 + data.length);
    chunk.writeUInt32BE(data.length, 0);
    chunk.write(type, 4, 'ascii');
    data.copy(chunk, 8);
    chunk.writeUInt32BE(crc32(chunk.subarray(4, 8 + data.length)), 8 + data.length);
    return chunk;
}

// Joins whole-image PNG frames into one PNG that plays them in a loop. A frame that stays the same is stored once.
export function animatedPng(frames) {
    if (!Array.isArray(frames) || !frames.length) throw new Error('Avatar frame is missing');
    const shown = [];
    for (const frame of frames) {
        const last = shown.at(-1);
        if (last?.image.equals(frame)) last.count++;
        else shown.push({ image: frame, count: 1, ...pngChunks(frame) });
    }
    if (shown.length === 1) return shown[0].image;
    const header = shown[0].header;
    if (shown.some(frame => !frame.header.equals(header))) throw new Error('Avatar frame sizes differ');
    const animation = Buffer.alloc(8);
    animation.writeUInt32BE(shown.length, 0);
    const parts = [PNG_SIGNATURE, pngChunk('IHDR', header), pngChunk('acTL', animation)];
    let sequence = 0;
    for (const [index, frame] of shown.entries()) {
        const control = Buffer.alloc(26);
        control.writeUInt32BE(sequence++, 0);
        header.copy(control, 4, 0, 8);
        control.writeUInt16BE(frame.count, 20);
        control.writeUInt16BE(FRAMES_PER_SECOND, 22);
        parts.push(pngChunk('fcTL', control));
        for (const data of frame.data) {
            if (index === 0) { parts.push(pngChunk('IDAT', data)); continue; }
            const numbered = Buffer.alloc(4 + data.length);
            numbered.writeUInt32BE(sequence++, 0);
            data.copy(numbered, 4);
            parts.push(pngChunk('fdAT', numbered));
        }
    }
    parts.push(pngChunk('IEND', Buffer.alloc(0)));
    return Buffer.concat(parts);
}

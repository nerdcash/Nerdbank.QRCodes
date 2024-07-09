use encoding::all::UTF_16LE;
use encoding::DecoderTrap;
use encoding::Encoding;
use image::{self, DynamicImage, ImageResult};
use rqrr::PreparedImage;

#[no_mangle]
pub extern "C" fn decode_qr_code(
    file_path: *const u16,
    file_path_len: usize,
    decoded: *mut u16,
    decoded_size: *mut usize,
) -> u32 {
    let file_path =
        unsafe { std::slice::from_raw_parts(file_path as *const u8, file_path_len * 2) };
    let file_path = match UTF_16LE.decode(file_path, DecoderTrap::Strict) {
        Ok(f) => f,
        Err(_) => return 1, // Error: Invalid UTF-16 string
    };

    decode_qr_code_interop_helper(image::open(file_path), decoded, decoded_size)
}

#[no_mangle]
pub extern "C" fn decode_qr_code_from_image(
    image_buffer: *const u8,
    image_buffer_len: usize,
    decoded: *mut u16,
    decoded_size: *mut usize,
) -> u32 {
    let image_buffer = unsafe { std::slice::from_raw_parts(image_buffer, image_buffer_len) };

    decode_qr_code_interop_helper(image::load_from_memory(image_buffer), decoded, decoded_size)
}

fn decode_qr_code_interop_helper(
    image: ImageResult<DynamicImage>,
    decoded: *mut u16,
    decoded_size: *mut usize,
) -> u32 {
    match decode_qr_code_helper(image) {
        Ok(content) => {
            let decoded_size = unsafe { &mut *decoded_size };
            let decoded_slice = unsafe { std::slice::from_raw_parts_mut(decoded, *decoded_size) };
            let content = content.encode_utf16();

            let mut content_len = 0;
            for (i, c) in content.enumerate() {
                if i >= *decoded_size {
                    return 2; // Error: Buffer size exceeded
                }
                decoded_slice[i] = c;
                content_len += 1;
            }
            *decoded_size = content_len;
            0
        }
        Err(e) => e,
    }
}

fn decode_qr_code_helper(image: ImageResult<DynamicImage>) -> Result<String, u32> {
    let image = image.map_err(|_e| 1u32)?;
    let image = image.to_luma8();
    let mut img = PreparedImage::prepare(image);
    let grids = img.detect_grids();
    let (_, content) = grids[0].decode().map_err(|_e| 2u32)?;
    Ok(content)
}

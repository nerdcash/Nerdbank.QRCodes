use encoding::{all::UTF_16LE, DecoderTrap, Encoding};
use image::{self, DynamicImage};
use rxing::{
    common::HybridBinarizer, BarcodeFormat, BinaryBitmap, BufferedImageLuminanceSource,
    DecodeHintValue, DecodeHints, Exceptions, MultiFormatReader, RXingResult, Reader,
};
use std::collections::HashSet;

const QR_DECODE_NO_QR_CODE: i32 = 0;
const INVALID_UTF16_STRING: i32 = -1;
const IMAGE_ERROR: i32 = -2;
const QR_DECODE_ERROR: i32 = -3;

#[no_mangle]
pub extern "C" fn decode_qr_code_from_file(
    file_path: *const u16,
    file_path_len: usize,
    decoded: *mut u16,
    decoded_length: usize,
) -> i32 {
    let file_path =
        unsafe { std::slice::from_raw_parts(file_path as *const u8, file_path_len * 2) };
    let file_path = match UTF_16LE.decode(file_path, DecoderTrap::Strict) {
        Ok(f) => f,
        Err(_) => return INVALID_UTF16_STRING,
    };

    process_result(
        rxing::helpers::detect_in_file(file_path.as_str(), Some(rxing::BarcodeFormat::QR_CODE)),
        decoded,
        decoded_length,
    )
}

#[no_mangle]
pub extern "C" fn decode_qr_code_from_image(
    image_buffer: *const u8,
    image_buffer_len: usize,
    decoded: *mut u16,
    decoded_length: usize,
) -> i32 {
    let image_buffer = unsafe { std::slice::from_raw_parts(image_buffer, image_buffer_len) };

    if let Ok(image) = image::load_from_memory(image_buffer) {
        process_result(
            detect_in_file_with_hints(image, Some(BarcodeFormat::QR_CODE)),
            decoded,
            decoded_length,
        )
    } else {
        IMAGE_ERROR
    }
}

fn process_result(
    r: Result<RXingResult, Exceptions>,
    decoded: *mut u16,
    decoded_length: usize,
) -> i32 {
    match r {
        Ok(r) => {
            let decoded_slice = unsafe { std::slice::from_raw_parts_mut(decoded, decoded_length) };
            let content = r.getText().encode_utf16();

            let mut content_len = 0;
            for (i, c) in content.enumerate() {
                if i < decoded_length {
                    decoded_slice[i] = c;
                }
                content_len += 1;
            }
            content_len
        }
        Err(e) => match e {
            rxing::Exceptions::NotFoundException(_) => QR_DECODE_NO_QR_CODE,
            _ => QR_DECODE_ERROR,
        },
    }
}

fn detect_in_file_with_hints(
    img: DynamicImage,
    barcode_type: Option<BarcodeFormat>,
) -> Result<RXingResult, Exceptions> {
    let mut multi_format_reader = MultiFormatReader::default();

    let mut hints = DecodeHints::default().with(DecodeHintValue::TryHarder(true));
    if let Some(bc_type) = barcode_type {
        hints = hints.with(DecodeHintValue::PossibleFormats(HashSet::from([bc_type])));
    }

    multi_format_reader.decode_with_hints(
        &mut BinaryBitmap::new(HybridBinarizer::new(BufferedImageLuminanceSource::new(img))),
        &hints,
    )
}

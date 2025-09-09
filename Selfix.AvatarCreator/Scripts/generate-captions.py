import os
from PIL import Image
from transformers import AutoProcessor, AutoModelForCausalLM
import torch

device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')

local_model_dir = "/workspace/models/captioning/florence2l"
model = AutoModelForCausalLM.from_pretrained(local_model_dir, trust_remote_code=True).to(device)
processor = AutoProcessor.from_pretrained(local_model_dir, trust_remote_code=True)

image_dir = '/workspace/input'
trigger_word = 'GNAVTRTKN'

def caption_image(task_prompt, image_path):
    image = Image.open(image_path)
    inputs = processor(text=task_prompt, images=image, return_tensors="pt")
    inputs = {key: value.to(device) for key, value in inputs.items()}
    generated_ids = model.generate(
        input_ids=inputs["input_ids"],
        pixel_values=inputs["pixel_values"],
        max_new_tokens=1024,
        early_stopping=False,
        do_sample=False,
        num_beams=3,
    )
    generated_text = processor.batch_decode(generated_ids, skip_special_tokens=False)[0]
    parsed_answer = processor.post_process_generation(
        generated_text,
        task=task_prompt,
        image_size=(image.width, image.height)
    )
    return parsed_answer


for filename in os.listdir(image_dir):
    if filename.lower().endswith(('.png', '.jpg', '.jpeg', '.bmp', '.gif')):
        image_path = os.path.join(image_dir, filename)
        result = caption_image('<DETAILED_CAPTION>', image_path)

        detailed_caption = result.get('<DETAILED_CAPTION>', '').replace('The image shows ', '')

        print(detailed_caption)

        result_path = os.path.splitext(image_path)[0] + '.txt'
        with open(result_path, 'w') as result_file:
            result_file.write(f"{trigger_word} {detailed_caption}")
        print(f'Processed and saved: {filename}')

print('All images processed.')
if torch.cuda.is_available():
    torch.cuda.empty_cache()
print('Cuda cache cleaned.')
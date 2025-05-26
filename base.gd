extends Node2D


# Called when the node enters the scene tree for the first time.
var im: Image
var tex: Sprite2D
var counter: float
var grid_size: int = 30;
var window_size: Vector2i = Vector2i(40,26) * grid_size
var marker_pos: Vector2i;
var origin: Vector2i
var shape: Array[Vector2i] = []
var old_window_size: Vector2i
func _ready() -> void:
	get_window().size = window_size
	marker_pos = window_size / 2
	origin = marker_pos
	# get_window().size = Vector2i(300,300)
	im = Image.new()
	tex = $Tex

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	counter += delta
	var radius: float = 40 * sin(counter)

	if Input.is_action_just_pressed("ui_left"):
		marker_pos.x -=  grid_size
	if Input.is_action_just_pressed("ui_right"):
		marker_pos.x +=  grid_size
	if Input.is_action_just_pressed("ui_up"):
		marker_pos.y -=  grid_size
	if Input.is_action_just_pressed("ui_down"):
		marker_pos.y +=  grid_size
	if Input.is_action_just_pressed("ui_accept"):
		shape.append(marker_pos)


	var shape_str: String = ""
	for p in shape:
		if shape_str.length() == 0:
			shape_str += "M {0} {1}\n".format([p.x, p.y])
		shape_str += "L {0} {1}\n".format([p.x, p.y])
	im.load_svg_from_string(
'<svg xmlns="http://www.w3.org/2000/svg" width="{0}" height="{1}">'.format([window_size.x,window_size.y]) + 
'<g transform="scale({0}) translate({1},{2}) rotate({3})">'.format([1,-(marker_pos.x - origin.x),-(marker_pos.y - origin.y),0]) + 
'
	<circle cx="'+ str(origin.x - 90) + '" cy="'+ str(origin.y)+'" r="2"/>
	<circle cx="'+ str(origin.x + 90) + '" cy="'+ str(origin.y)+'" r="2"/>
	<circle cx="'+ str(origin.x) + '" cy="'+ str(origin.y - 90)+'" r="2"/>
	<circle cx="'+ str(origin.x) + '" cy="'+ str(origin.y + 90)+'" r="2"/>
	<path
		d="' + shape_str + '"
		stroke="black"
		fill="gray"
		stroke-width="3"
		fill-opacity="0.5" />
</g>
</svg>')
	tex.texture = ImageTexture.create_from_image(im)
	queue_redraw()

	
func _draw() -> void:
	var vp: Vector2 = get_window().size
	for i in range(vp.y / grid_size + 1):
		draw_line(Vector2(0,i * grid_size),Vector2(3000,i*grid_size),Color.from_rgba8(0,0,0,30),0.5,true);

	for i in range(vp.x / grid_size + 1):
		draw_line(Vector2(i * grid_size,0),Vector2(i*grid_size,3000),Color.from_rgba8(0,0,0,30),0.5,true);
	
	draw_circle(origin, 10, Color.from_rgba8(200,0,0,150),false, 1.5,true)



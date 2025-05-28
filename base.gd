extends Node2D

enum PointType {WHOLE, BROKEN}
enum SegmentType {STRAIGHT, CUBIC}

# testing

class Segment:
	var type: SegmentType = SegmentType.STRAIGHT
	var inPoint: Point
	var outPoint: Point
	var handles: Array[Handle] = []

	func _init(inpPos: Vector2i, outpPos: Vector2i, t: SegmentType):
		inPoint = Point.new(inpPos)
		outPoint = Point.new(outpPos)
		type = t
		if t == SegmentType.CUBIC:
			makeCubic()
	
	func pointPositions() -> Array[Vector2i]:
		var pps: Array[Vector2i] = [inPoint.pos, outPoint.pos]
		for h: Handle in handles:
			pps.append(h.pos)
		return pps

	func makeCubic():
		if type != SegmentType.CUBIC:
			type = SegmentType.CUBIC
		var inpPos = inPoint.pos
		var outpPos = outPoint.pos
		var average: Vector2i = (inpPos + outpPos) / 2
		var inHandle: Handle = Handle.new((average + inpPos) / 2.0)
		var outHandle: Handle = Handle.new((average + outpPos) / 2.0)
		handles = [inHandle, outHandle]
	
	func avPos() -> Vector2i:
		var outv: Vector2i = Vector2i(0,0)
		var pps = pointPositions()
		for p in pps:
			outv += p
		outv /= len(pps)
		return outv

	func toSVG(reverse: bool = false) -> String:
		var s: String = ''
		if type == SegmentType.CUBIC:
			s += 'C '
			if reverse:
				s += str(handles[0].pos.x) + " " + str(handles[0].pos.y) + ", "
				s += str(handles[1].pos.x) + " " + str(handles[1].pos.y) + ", "
				s += str(inPoint.pos.x) + " " + str(inPoint.pos.y) + " "
			else:
				s += str(handles[1].pos.x) + " " + str(handles[1].pos.y) + ", "
				s += str(handles[0].pos.x) + " " + str(handles[0].pos.y) + ", "
				s += str(outPoint.pos.x) + " " + str(outPoint.pos.y) + " "
		elif type == SegmentType.STRAIGHT:
			s += 'L '
			s += str(outPoint.pos.x) + " " + str(outPoint.pos.y) + " "
		return s


class Handle:
	var pos: Vector2i
	func _init(p=Vector2i(0,0)):
		pos = p


class Point:
	var pos: Vector2i
	var type: PointType
	func _init(p=Vector2i(0,0), t = PointType.BROKEN):
		pos = p
		type = t


class Shape:
	var belongsTo: Shapes = null 
	var points: Array[Point]
	var segments: Array[Segment]
	var closed: bool

	func _init(p: Array[Point]=[], s: Array[Segment]=[], c: bool = false):
		points = p
		segments = s
		closed = c

	func add_belongs_too(sps: Shapes):
		belongsTo = sps

	func add_point(p: Point) -> void:
		if len(points) > 0 and points[-1].pos == p.pos:
			points[-1] = p
		else:
			points.append(p)
			if len(points) > 1:
				var s: Segment = Segment.new(points[-2].pos, p.pos, SegmentType.CUBIC)
				segments.append(s)

		print(len(points))

	func delete():
		print("deleting")
		belongsTo.remove_shape(self)

	func close():
		closed = true	
		if len(points) < 3:
			delete()
		else:
			var s: Segment = Segment.new(points[-1].pos, points[0].pos, SegmentType.CUBIC)
			segments.append(s)


	func shape_string() -> String:
		var shape_str: String = ""
		for s in segments:
			if shape_str.length() == 0:
				shape_str += "M {0} {1} ".format([s.inPoint.pos.x, s.inPoint.pos.y])
			shape_str += s.toSVG()
		if closed:
			shape_str += 'Z\n'
		return shape_str


class Shapes:
	var shapes: Array[Shape] = []

	func add_shape(s: Shape):
		var to_append: Shape = Shape.new(s.points, s.segments, s.closed) 	
		if len(to_append.points) > 2:
			to_append.add_belongs_too(self)
			to_append.close()
			shapes.append(to_append)
	
	func remove_shape(s: Shape):
		shapes.erase(s)


# Called when the node enters the scene tree for the first time.
const borders: Vector2i = Vector2i(60,20)
const far_move_border: int = 2 * grid_size
const grid_size: int = 10;
var im: Image
var im_magnified: Image
var tex: Sprite2D
var tex_magnified: Sprite2D
var counter: float
var window_size: Vector2i = Vector2i(100,80) * grid_size
var marker_pos: Vector2i;
var origin: Vector2i
var shape: Shape = Shape.new() 
var shapes: Shapes = Shapes.new()
var old_window_size: Vector2i
var preview: bool = false
var held_time: Dictionary = {"left" : 0, "right" : 0, "up" : 0, "down" : 0}
var held_time2: float = 0
var valid_hold_length = .2

func _ready() -> void:
	get_window().size = window_size
	marker_pos = window_size / 2
	origin = marker_pos
	# get_window().size = Vector2i(300,300)
	im = Image.new()
	im_magnified = Image.new()
	tex = $Tex
	tex_magnified = $Magnified

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	$Magnified.visible = false
	preview = false
	counter += delta
	var radius: float = 40 * sin(counter)

	if Input.is_key_pressed(KEY_TAB):
		preview = true
	if Input.is_action_just_pressed("ui_cancel"):
		get_tree().quit()

	var movement_amount: int = grid_size
	if Input.is_key_pressed(KEY_SHIFT):
		movement_amount = grid_size * 12

	if Input.is_key_pressed(KEY_A):
		movement_amount = 1
		$Magnified.visible = true

	if !Input.is_action_pressed("ui_left") and !Input.is_action_pressed("ui_right") and !Input.is_action_pressed("ui_down") and !Input.is_action_pressed("ui_up"): 
		held_time2 = 0
	if Input.is_action_just_pressed("ui_left"):
		# held_time["left"] = 0
		marker_pos.x -=  movement_amount
	if Input.is_action_just_pressed("ui_right"):
		# held_time["right"] = 0
		marker_pos.x +=  movement_amount
	if Input.is_action_just_pressed("ui_up"):
		# held_time["up"] = 0
		marker_pos.y -=  movement_amount
	if Input.is_action_just_pressed("ui_down"):
		# held_time["down"] = 0
		marker_pos.y +=  movement_amount

	if Input.is_action_pressed("ui_left"): 
		if held_time2 > valid_hold_length:
			marker_pos.x -=  movement_amount
		else:
			held_time2 += delta
		# if held_time["left"] > valid_hold_length:
		# 	marker_pos.x -=  movement_amount
		# else:
		# 	held_time["left"] += delta
	if Input.is_action_pressed("ui_right"): 
		if held_time2 > valid_hold_length:
			marker_pos.x +=  movement_amount
		else:
			held_time2 += delta
		# if held_time["right"] > valid_hold_length:
		# 	marker_pos.x +=  movement_amount
		# else:
		# 	held_time["right"] += delta
	if Input.is_action_pressed("ui_up"): 
		if held_time2 > valid_hold_length:
			marker_pos.y -=  movement_amount
		else:
			held_time2 += delta
		# if held_time["up"] > valid_hold_length:
		# 	marker_pos.y -=  movement_amount
		# else:
		# 	held_time["up"] += delta
	if Input.is_action_pressed("ui_down"): 
		if held_time2 > valid_hold_length:
			marker_pos.y +=  movement_amount
		else:
			held_time2 += delta
		# if held_time["down"] > valid_hold_length:
		# 	marker_pos.y +=  movement_amount
		# else:
		# 	held_time["down"] += delta

	marker_pos = check_borders(marker_pos)

	if Input.is_action_just_pressed("add_new_point"):
		shape.add_point(Point.new(marker_pos))
	if Input.is_action_just_pressed("finish_shape"):
		shapes.add_shape(shape)
		shape = Shape.new()


	var last_point_placed: String = ""
	if len(shape.points) > 0:
		last_point_placed += '<circle cx="{0}" cy="{1}" r="10" stroke="blue" fill-opacity=".0" stroke-width=".5"/>'.format([shape.points[-1].pos.x, shape.points[-1].pos.y])

	var shape_closed_look: String = '
		stroke="black"
		fill="green"
		stroke-width="2"
		fill-opacity="0.5"
	'
	var shape_open_look: String = '
		stroke="black"
		fill="red"
		stroke-width="2"
		fill-opacity="0.5"
	'
	if preview:
		var shape_look = '
			stroke="black"
			fill="white"
			stroke-width="0"
			fill-opacity="2"
		'
		shape_closed_look = shape_look
		shape_open_look = shape_look
		

	var svg_to_draw: String = (
'<svg xmlns="http://www.w3.org/2000/svg" width="{0}" height="{1}">'.format([window_size.x,window_size.y]) + 
'<g transform="scale({0}) translate({1},{2}) rotate({3})">'.format([1,0,0,0])) 
	if !preview:
		var guideLines: int = 12
		svg_to_draw += (
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=.3 stroke-width="1"/>
		'.format([origin.y - guideLines*grid_size])
		+ 
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=".3" stroke-width="1"/>
		'.format([origin.y + guideLines*grid_size])
		 +
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=.3 stroke-width="1"/>
		'.format([origin.y - guideLines*2*grid_size])
		+ 
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=".3" stroke-width="1"/>
		'.format([origin.y + guideLines*2*grid_size])
		)
	for s in shapes.shapes:
		var visible_point_size: int = 10
		if !preview:
			for p in s.points:
				if p.type == PointType.WHOLE: 
					svg_to_draw += (
						'<circle cx="' + str(p.pos.x) + '" cy="' + str(p.pos.y) + '" r="' + str(visible_point_size / 2.) + '" fill-opacity="0.0" stroke-opacity="0.5" stroke-width="2" stroke="black"/>'
					)
				elif p.type == PointType.BROKEN:
					var p1 = str(p.pos.x) + "," + str(p.pos.y + .55*visible_point_size)
					var p2 = str(p.pos.x - .5 * visible_point_size) + "," + str(p.pos.y - .33*visible_point_size)
					var p3 = str(p.pos.x + .5 * visible_point_size) + "," + str(p.pos.y - .33*visible_point_size)
					svg_to_draw += (
						'<polygon points="' + p1 + ' ' +  p2 + ' ' + p3 + '" fill-opacity="0.0" stroke-opacity="0.5" stroke-width="2" stroke="black"/>'
					)
			for seg in s.segments:
				for h in seg.handles: 
					svg_to_draw += (
						'<circle cx="' + str(h.pos.x) + '" cy="' + str(h.pos.y) + '" r="' + str(visible_point_size / 2.) + '" fill-opacity="0.0" stroke-opacity="0.5" stroke-width="2" stroke="black"/>'
					)
		svg_to_draw += (
		'<path
			d="' + s.shape_string() + '"' + 
			shape_closed_look
			+ '/>'
		)

	svg_to_draw += (
	'<path
		d="' + shape.shape_string() + '"' + 
		shape_open_look
		+ '/>'
	)
	svg_to_draw += last_point_placed
	svg_to_draw += '</g></svg>'

	var svg_magnified: String = (
'<svg xmlns="http://www.w3.org/2000/svg" width="{0}" height="{1}">'.format([300,300]) + 
'<g transform="scale({0}) translate({1},{2}) rotate({3})">'.format([3.0,-(marker_pos.x - 50),-(marker_pos.y - 50) ,0])) 
	if !preview:
		svg_magnified += (
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=.3 stroke-width="1"/>
		'.format([origin.y - 12*grid_size])
		+ 
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=".3" stroke-width="1"/>
		'.format([origin.y + 12*grid_size])
		)
	for s in shapes.shapes:
		svg_magnified += (
		'<path
			d="' + s.shape_string() + '"' + 
			'stroke="black"
			fill="blue"
			stroke-width=".5"
			fill-opacity="0.5"'
			+ '/>'
		)

	svg_magnified += (
	'<path
		d="' + shape.shape_string() + '"' + 
		'stroke="black"
		fill="red"
		stroke-width=".5"
		fill-opacity="0.5"'
		+ '/>'
	)

	svg_magnified += '<circle cx="{0}" cy="{1}" r="3" stroke="red" stroke-width=".5" fill-opacity="0"/>'.format([(marker_pos.x),(marker_pos.y)])
	svg_magnified += '</g></svg>'
	
	im.load_svg_from_string(svg_to_draw)
	tex.texture = ImageTexture.create_from_image(im)
	im_magnified.load_svg_from_string(svg_magnified)
	tex_magnified.texture = ImageTexture.create_from_image(im_magnified)
	queue_redraw()

	
func _draw() -> void:
	if preview:
		return
	var vp: Vector2 = get_window().size
	for i in range(vp.y / grid_size + 1):
		draw_line(Vector2(0,i * grid_size),Vector2(3000,i*grid_size),Color.from_rgba8(0,0,0,10),0.5,true);

	for i in range(vp.x / grid_size + 1):
		draw_line(Vector2(i * grid_size,0),Vector2(i*grid_size,3000),Color.from_rgba8(0,0,0,10),0.5,true);
	var crosslength: int = 3
	draw_line(Vector2(origin.x - grid_size*crosslength, origin.y),Vector2(origin.x+grid_size*crosslength,origin.y),Color.from_rgba8(0,0,0,30),0.5,true);
	draw_line(Vector2(origin.x, origin.y - grid_size*crosslength),Vector2(origin.x, origin.y+grid_size*crosslength),Color.from_rgba8(0,0,0,30),0.5,true);
	
	draw_circle(marker_pos, 10, Color.from_rgba8(200,0,0,150),false, 1.5,true)

func check_borders(mpos: Vector2i) -> Vector2i:
	var new_mpos: Vector2i = mpos
	var wsize = get_window().size
	if mpos.x < borders.x:
		new_mpos.x = borders.x
	elif mpos.x > wsize.x - borders.x:
		new_mpos.x = wsize.x - borders.x
	if mpos.y < borders.y:
		new_mpos.y = borders.y
	elif mpos.y > wsize.y - borders.y:
		new_mpos.y = wsize.y - borders.y

	return new_mpos
		

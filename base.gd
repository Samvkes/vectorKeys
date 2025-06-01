extends Node2D

enum PointType {WHOLE, BROKEN}
enum SegmentType {STRAIGHT, CUBIC}

# testing

class Segment:
	var type: SegmentType = SegmentType.STRAIGHT
	var inPoint: Point
	var outPoint: Point
	var handles: Array[Handle] = []
	static var already_affected_this_frame: Array = []

	func c() -> String:
		return "Segment"
	
	func _init(inP: Point, outP: Point, t: SegmentType):
		inPoint = inP
		outPoint = outP
		inPoint.add_adjacent_segment(self)
		outPoint.add_adjacent_segment(self)
		type = t
		if t == SegmentType.CUBIC:
			makeCubic()
	
	func pointPositions() -> Array[Vector2i]:
		var pps: Array[Vector2i] = [inPoint.pos, outPoint.pos]
		for h: Handle in handles:
			pps.append(h.pos)
		return pps
	
	func getPos() -> Vector2i:
		return avPos()

	func setPos(change: Vector2i):
		var allPoints = [inPoint, outPoint]
		for p in allPoints:
			if p not in Globl.currently_selected and p not in already_affected_this_frame:
				already_affected_this_frame.append(p)
				p.setPos(change)
		
	func switch_segment_type():
		if type == SegmentType.STRAIGHT:
			makeCubic()
		else:
			makeStraight()

	func makeStraight():
		if type != SegmentType.STRAIGHT:
			type = SegmentType.STRAIGHT
		for h in handles:
			h.delete()
		handles = []

	func makeCubic():
		if type != SegmentType.CUBIC:
			type = SegmentType.CUBIC
		var inpPos = inPoint.pos
		var outpPos = outPoint.pos
		var average: Vector2i = (inpPos + outpPos) / 2
		var inHandle: Handle = Handle.new((average + outpPos) / 2.0)
		var outHandle: Handle = Handle.new((average + inpPos) / 2.0)
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
	var selection_key: String
	var selectable: bool
	var next_to_whole_point: bool

	func c() -> String:
		return "Handle"

	func _init(p=Vector2i(0,0), sb:bool = true):
		pos = p
		selectable = sb
		if selectable:
			var sk = Globl.add_to_possible_selections(self)
			selection_key = sk

	func delete():
		Globl.currently_selected.erase(self)
		Globl.possible_selections_dict.erase(selection_key)

	func getPos() -> Vector2i:
		return pos

	func setPos(change: Vector2i):
		pos += change



class Point:
	var pos: Vector2i
	var type: PointType
	var adjacent_segments: Array[Segment] = []

	func c() -> String:
		return "Point"

	func _init(p=Vector2i(0,0), t = PointType.BROKEN):
		pos = p
		type = t
	
	func switch_point_type():
		if type == PointType.WHOLE:
			type = PointType.BROKEN
		elif type == PointType.BROKEN:
			type = PointType.WHOLE
			mend()
	
	func mend():
		var h = adjacent_handles()
		if len(h) == 1:
			if len(adjacent_segments) > 1:
				var to_align_with: Segment
				if h[0] in adjacent_segments[0].handles:
					to_align_with = adjacent_segments[1]
				else:
					to_align_with = adjacent_segments[0]
				h[0].pos = Globl.project_point_on_line(h[0].getPos(), to_align_with.inPoint.pos, to_align_with.outPoint.pos)
				
	func delete():
		Globl.currently_selected.erase(self)
		Globl.possible_selections_dict.erase(self)
	
	func add_adjacent_segment(ads: Segment):
		assert(len(adjacent_segments) < 2)
		adjacent_segments.append(ads)

	func adjacent_handles() -> Array[Handle]: 
		var ah: Array[Handle] = []
		for s in adjacent_segments:
			if len(s.handles) > 0: 
				if s.inPoint == self:
					ah.append(s.handles[1])
				else:
					ah.append(s.handles[0])
		return ah

	func getPos() -> Vector2i:
		return pos

	func setPos(change: Vector2i):
		pos += change
		for h in adjacent_handles():
			if h not in Globl.currently_selected:
				h.setPos(change)


class Shape:
	var belongsTo: Shapes = null 
	var points: Array[Point]
	var segments: Array[Segment]
	var closed: bool

	func c() -> String:
		return "Shape"

	func _init(p: Array[Point]=[], s: Array[Segment]=[], clo: bool = false):
		points = p
		segments = s
		closed = clo

	func add_belongs_too(sps: Shapes):
		belongsTo = sps

	func add_point(m_pos: Vector2i) -> void:
		var p = Point.new(m_pos)
		Globl.add_to_possible_selections(p)
		if len(points) > 0 and points[-1].pos == p.pos:
			points[-1] = p
		else:
			points.append(p)
			if len(points) > 1:
				var s: Segment = Segment.new(points[-2], p, SegmentType.STRAIGHT)
				segments.append(s)
				Globl.add_to_possible_selections(s)
		print(len(points))


	func delete():
		print("deleting shape")
		belongsTo.remove_shape(self)

	func close():
		closed = true	
		if len(points) < 3:
			delete()
		else:
			var s: Segment = Segment.new(points[-1], points[0], SegmentType.STRAIGHT)
			segments.append(s)
			Globl.add_to_possible_selections(s)
	
	func getPos() -> Vector2i:
		var aver: Vector2i = Vector2i(0,0)
		for p in points:
			aver += p.pos
		aver /= len(points)
		return aver
	
	func setPos(change: Vector2i):
		for s in segments:
			if s not in Globl.currently_selected:
				s.setPos(change)

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

	func c() -> String:
		return "Shapes"

	func add_shape(s: Shape):
		var to_append: Shape = Shape.new(s.points, s.segments, s.closed) 	
		if len(to_append.points) > 2:
			to_append.add_belongs_too(self)
			to_append.close()
			shapes.append(to_append)
			Globl.add_to_possible_selections(to_append)
	
	func remove_shape(s: Shape):
		shapes.erase(s)


# Called when the node enters the scene tree for the first time.
const borders: Vector2i = Vector2i(0,0)
const far_move_border: int = 2 * grid_size
const grid_size: int = 12
var grid_modifier: float = 2
var back_color: Color = Color.from_ok_hsl(56/359.0, 23/100.0, 56/100.0)
var back_color_preview: Color = Color.from_ok_hsl(261/359.0, 73/100.0, 30/100.0)
# var back_color_preview: Color = Color.from_ok_hsl(264/359.0, 100/100.0, 47/100.0)
var ManagerScript 
var manager_node
var im: Image
var im_magnified: Image
var tex: Sprite2D
var tex_magnified: Sprite2D
var counter: float
var window_size: Vector2i = Vector2i(60,80) * grid_size
var marker_pos: Vector2i;
var origin: Vector2i
var shape: Shape = Shape.new() 
var shapes: Shapes = Shapes.new()
var old_window_size: Vector2i
var keys_pressed_array: Array[String] = []
var zoom: float = 1

var preview: bool = false
var select_mode: bool = false
var quit_select_mode: bool = false
# var possible_selections_dict: Dictionary = {}
# var currently_selected: Array = []

var held_time: Dictionary = {"left" : 0, "right" : 0, "up" : 0, "down" : 0}
var held_time2: float = 0
var valid_hold_length = .2
var default_font
var default_font_size 

func _ready() -> void:
	ManagerScript = load("res://Manager.cs")
	manager_node = ManagerScript.new() 
	add_child(manager_node)
		
	default_font = ThemeDB.fallback_font
	default_font_size = 10
	get_window().size = window_size
	marker_pos = window_size / 2
	origin = marker_pos
	im = Image.new()
	im_magnified = Image.new()
	tex = $Tex
	tex_magnified = $Magnified

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	Segment.already_affected_this_frame = []
	$Magnified.visible = false
	counter += delta
	var radius: float = 40 * sin(counter)

	if Input.is_action_just_pressed("toggle_preview"):
		preview = !preview
	if Input.is_key_pressed(KEY_BACKSPACE):
		get_tree().quit()

	var back: ColorRect = $Background
	if preview:
		back.color = back_color_preview
	else:
		back.color = back_color

	handle_input(delta)

	var sw: float = 2
	var visible_point_size: int = 10
	if zoom > 1:
		visible_point_size = 4
		sw = .5

	var last_point_placed: String = ""
	if len(shape.points) > 0:
		last_point_placed += '<circle cx="{0}" cy="{1}" r="{2}" stroke="blue" fill-opacity=".0" stroke-width="{3}"/>'.format([shape.points[-1].pos.x, shape.points[-1].pos.y, visible_point_size-2, sw * .5])

	var shape_closed_look: String = '
		stroke="black"
		fill="green"
		stroke-width="'+str(sw)+'"
		fill-opacity="0.5"
	'
	var shape_open_look: String = '
		stroke="black"
		fill="red"
		stroke-width="'+str(sw)+'"
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
'<g transform="scale({0}) translate({1},{2}) rotate({3})">'.format([1,marker_pos.x,marker_pos.y,0]) + 
'<g transform="scale({0}) translate({1},{2}) rotate({3})">'.format([zoom,-marker_pos.x,-marker_pos.y,0])) 
	if !preview:
		var guideLinesH: int = 16
		var guideLinesV: int = 16
		svg_to_draw += (
		'
		<path d="M {0} 0 V 2000" stroke="black" stroke-opacity=.3 stroke-width="1"/>
		'.format([origin.x - guideLinesV*grid_size])
		+ 
		'
		<path d="M {0} 0 V 2000" stroke="black" stroke-opacity=.3 stroke-width="1"/>
		'.format([origin.x + guideLinesV*grid_size])
		+ 
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=.3 stroke-width="1"/>
		'.format([origin.y - guideLinesH*grid_size])
		+ 
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=".3" stroke-width="1"/>
		'.format([origin.y + guideLinesH*grid_size])
		 +
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=.3 stroke-width="1"/>
		'.format([origin.y - guideLinesH*2*grid_size])
		+ 
		'
		<path d="M 0 {0} H 2000" stroke="black" stroke-opacity=".3" stroke-width="1"/>
		'.format([origin.y + guideLinesH*2*grid_size])
		)
	for s in shapes.shapes:
		if !preview:
			for p in s.points:
				if p.type == PointType.WHOLE: 
					svg_to_draw += (
						'<circle cx="' + str(p.pos.x) + '" cy="' + str(p.pos.y) + '" r="' + str(visible_point_size / 2.) + '" fill-opacity="0.0" stroke-opacity="0.5" stroke-width="'+str(sw)+'" stroke="black"/>'
					)
				elif p.type == PointType.BROKEN:
					var p1 = str(p.pos.x) + "," + str(p.pos.y + .55*visible_point_size)
					var p2 = str(p.pos.x - .5 * visible_point_size) + "," + str(p.pos.y - .33*visible_point_size)
					var p3 = str(p.pos.x + .5 * visible_point_size) + "," + str(p.pos.y - .33*visible_point_size)
					svg_to_draw += (
						'<polygon points="' + p1 + ' ' +  p2 + ' ' + p3 + '" fill-opacity="0.0" stroke-opacity="0.5" stroke-width="'+str(sw)+'" stroke="black"/>'
					)
			for seg in s.segments:
				for h in seg.handles: 
					svg_to_draw += (
						'<circle cx="' + str(h.pos.x) + '" cy="' + str(h.pos.y) + '" r="' + str(visible_point_size / 2.) + '" fill-opacity="0.0" stroke-opacity="0.5" stroke-width="'+str(sw)+'" stroke="black"/>'
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
	svg_to_draw += '</g></g></svg>'

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
	if grid_modifier >= .5:	
		for i in range(vp.y / (grid_size * grid_modifier) + 1):
			draw_line(Vector2(0,i * grid_modifier*zoom * grid_size),Vector2(3000,i*grid_modifier *zoom* grid_size),Color.from_rgba8(0,0,0,10),0.5,true);

		for i in range(vp.x / grid_size + 1):
			draw_line(Vector2(i * grid_modifier * zoom * grid_size,0),Vector2(i*grid_modifier * zoom * grid_size,3000),Color.from_rgba8(0,0,0,10),0.5,true);
	var crosslength: int = 3
	draw_line(Vector2(origin.x - grid_size*crosslength, origin.y),Vector2(origin.x+grid_size*crosslength,origin.y),Color.from_rgba8(0,0,0,30),0.5,true);
	draw_line(Vector2(origin.x, origin.y - grid_size*crosslength),Vector2(origin.x, origin.y+grid_size*crosslength),Color.from_rgba8(0,0,0,30),0.5,true);
	
	draw_circle(marker_pos, 10, Color.from_rgba8(200,0,0,150),false, 1.5,true)
	# draw_string(default_font, marker_pos, str(marker_pos - origin), HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size)
	draw_string(default_font, marker_pos, str(marker_pos), HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size)

	# for selectable in Globl.currently_selected:
	# 	draw_circle(selectable.getPos() - Vector2i(-1,0), 10, Color.from_rgba8(250,250,250,150),true, -1.0,true)

	if select_mode:
		for sel_text in Globl.possible_selections_dict:
			var col: Color = Color.WHITE
			var selectable = Globl.possible_selections_dict[sel_text]
			var p: Vector2i = selectable.getPos()
			p -= marker_pos
			p = Vector2i(p.x * zoom, p.y * zoom)
			p += marker_pos
			p -= Vector2i(4,-5)
			draw_line(p - Vector2i(-5,5), p,Color.from_rgba8(200,0,0,90),1.0,true);
			draw_circle(p - Vector2i(-4,4), 7, Color.from_rgba8(50,50,50,150),true, -1.0,true)
			if selectable in Globl.currently_selected:
				col = Color.BLACK
			draw_string(default_font, p, sel_text, HORIZONTAL_ALIGNMENT_RIGHT, -1, default_font_size + 4, col)
	else:
		for selectable in Globl.currently_selected:
			var p: Vector2i = selectable.getPos()
			p -= marker_pos
			p = Vector2i(p.x * zoom, p.y * zoom)
			p += marker_pos
			draw_circle(p, 7, Color.from_rgba8(50,50,50,150),true, -1.0,true)


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
		

func _input(event: InputEvent) -> void:
	if event is InputEventKey:
		if event.pressed:
			if select_mode:
				if event.as_text() == 'Space':
					handle_selection_text(keys_pressed_array.duplicate())
					keys_pressed_array= []
				elif event.as_text() == 'Semicolon':
						select_mode = false
						print("exiting select mode")
						print("currently selected: " + str(Globl.currently_selected))
					
				else:
					quit_select_mode = false
					keys_pressed_array.append(event.as_text())
				print(keys_pressed_array)
			elif event.as_text() == 'Semicolon':
				select_mode = true
				keys_pressed_array = []
				print("entering select mode")

			if event.as_text() == 'Shift+Semicolon':
				Globl.currently_selected = []
				keys_pressed_array = []


func handle_selection_text(kpa: Array[String]):
	print("handling this possible selection:" + str(kpa))
	var s: String = ""
	for st in kpa:
		s += st
	s = s.to_lower()
	if s in Globl.possible_selections_dict:
		var sel = Globl.possible_selections_dict[s]
		if sel in Globl.currently_selected:
			Globl.currently_selected.erase(sel)
		else:
			Globl.currently_selected.append(sel)


func handle_input(delta: float):
	if !select_mode:
		hi_movement(delta)
		hi_point_adding(delta)



func hi_movement(delta: float):
	var movement_amount: int = grid_size * grid_modifier
	var moving_selected: Vector2i = Vector2i(0,0)
	if Input.is_key_pressed(KEY_SHIFT):
		movement_amount = grid_size * 12

	if Input.is_key_pressed(KEY_A):
		movement_amount = 1
		$Magnified.visible = true

	if !Input.is_action_pressed("ui_left") and !Input.is_action_pressed("ui_right") and !Input.is_action_pressed("ui_down") and !Input.is_action_pressed("ui_up"): 
		held_time2 = 0

	if Input.is_action_just_pressed("ui_left"):
		moving_selected.x -= movement_amount
	if Input.is_action_just_pressed("ui_right"):
		moving_selected.x += movement_amount
	if Input.is_action_just_pressed("ui_up"):
		moving_selected.y -= movement_amount 
	if Input.is_action_just_pressed("ui_down"):
		moving_selected.y += movement_amount 

	if Input.is_action_pressed("ui_left"): 
		if held_time2 > valid_hold_length:
			moving_selected.x -= movement_amount
		else:
			held_time2 += delta
	if Input.is_action_pressed("ui_right"): 
		if held_time2 > valid_hold_length:
			moving_selected.x += movement_amount
		else:
			held_time2 += delta
	if Input.is_action_pressed("ui_up"): 
		if held_time2 > valid_hold_length:
			moving_selected.y -= movement_amount
		else:
			held_time2 += delta
	if Input.is_action_pressed("ui_down"): 
		if held_time2 > valid_hold_length:
			moving_selected.y += movement_amount
		else:
			held_time2 += delta
	if len(Globl.currently_selected) > 0:
		for sel in Globl.currently_selected:
			sel.setPos(moving_selected)
	else:
		marker_pos += moving_selected
	marker_pos = check_borders(marker_pos)

func hi_point_adding(delta: float): 
	if Input.is_action_just_pressed("snap_selected"):
		snap_selected_pos()

	if Input.is_action_just_pressed("increase_zoom"):
		if zoom >= 1 and zoom < 4:
			zoom *= 4
		elif zoom < 1:
			zoom *= 2
	if Input.is_action_just_pressed("decrease_zoom"):
		if zoom <= 1 and zoom > .5:
			zoom /= 2
		elif zoom > 1:
			zoom /= 4
	if Input.is_action_just_pressed("increase_grid_modifier"):
		if grid_modifier < .5:
			grid_modifier = .5
			snap_marker_pos()
		elif grid_modifier < 8:
			grid_modifier *= 2
			snap_marker_pos()
	if Input.is_action_just_pressed("decrease_grid_modifier"):
		if grid_modifier > .5:
			grid_modifier /= 2
			snap_marker_pos()
		else:
			grid_modifier = 1.0 / grid_size
	if Input.is_action_just_pressed("switch_segment_style"):
		for sel: Object in Globl.currently_selected:
			if sel.c() == "Segment":
				sel.switch_segment_type()

	if Input.is_action_just_pressed("switch_point_style"):
		for sel: Object in Globl.currently_selected:
			if sel.c() == "Point":
				sel.switch_point_type()


	if Input.is_action_just_pressed("add_new_point"):
		shape.add_point(marker_pos)
		manager_node.PlaySound("Laptop_Keystroke_82.wav", 0.2, 1.3, 1.8)
		
	if Input.is_action_just_pressed("finish_shape"):
		shapes.add_shape(shape)
		shape = Shape.new()
		manager_node.PlaySound("camera.wav", 0.4, 1.3, 1.8)

func snap_marker_pos():
	var tot: int = grid_modifier * grid_size
	marker_pos.x = (marker_pos.x / (tot)) * tot
	marker_pos.y = (marker_pos.y / (tot)) * tot

func snap_selected_pos():
	var tot: int = grid_modifier * grid_size
	for sel in Globl.currently_selected:
		var p = sel.getPos()
		sel.setPos(Vector2i((p.x/tot) * tot, (p.y/tot) * tot) - p)

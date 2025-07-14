extends Node2D

enum PointType {WHOLE, BROKEN}
enum SegmentType {STRAIGHT, CUBIC}
enum PointSubdiv {NONE, CHAMFER, ROUNDED}

# testing

class FlatSegment:
	var inPoint: Vector2
	var outPoint: Vector2
	var inHandle: Vector2
	var outHandle: Vector2

	func c() -> String:
		return "FlatSegment"
	
	func _init(flatPositions: Array = []):
		if len(flatPositions) == 4:
			inPoint = flatPositions[0]
			inHandle = flatPositions[1]
			outHandle = flatPositions[2]
			outPoint = flatPositions[3]

	func reverse_segment():
		var tempPoint = inPoint
		inPoint = outPoint
		outPoint = tempPoint
		tempPoint = inHandle
		inHandle = outHandle
		outHandle = tempPoint

	func print():
		print("flatseg: " + str(inPoint / 128) + str(inHandle / 128) + str(outHandle/128) + str(outPoint/128))

	func addHandles():
		var VectorBetweenPoints = inPoint - outPoint
		inHandle = outPoint + VectorBetweenPoints.normalized()
		outHandle = inPoint - VectorBetweenPoints.normalized()

	func initFromPoints(inp: Point, outp: Point, hands: Array = []):
		inPoint = inp.pos
		outPoint = outp.pos
		var VectorBetweenPoints = inPoint - outPoint
		if len(hands) == 2:
			inHandle = hands[1].pos
			outHandle = hands[0].pos
		else:
			inHandle = outPoint + VectorBetweenPoints.normalized() * 100
			outHandle = inPoint - VectorBetweenPoints.normalized() * 100

	func toSVG() -> String:
		var s: String = ''
		s += 'C '
		s += str(inHandle.x) + " " + str(inHandle.y) + ", "
		s += str(outHandle.x) + " " + str(outHandle.y) + ", "
		s += str(outPoint.x) + " " + str(outPoint.y) + " "
		return s

	func pointPositionsFlat() -> Array[float]:
		var pps: Array[float] = [inPoint[0], inPoint[1]]
		var hInverse: Array[Vector2] = [inHandle, outHandle]
		for h: Vector2 in hInverse:
			pps.append(h[0])
			pps.append(h[1])
		pps.append(outPoint[0])
		pps.append(outPoint[1])
		return pps

class Segment:
	var selected: bool = false
	# var select_label: String = ""
	var myShape: Shape
	var type: SegmentType = SegmentType.STRAIGHT
	var inPoint: Point
	var outPoint: Point
	var handles: Array[Handle] = []
	var should_be_reversed: bool = false
	static var already_affected_this_frame: Array = []

	func c() -> String:
		return "Segment"

	func reverse_segment():
		var tempPoint = inPoint
		inPoint = outPoint
		outPoint = tempPoint
		handles.reverse()
		for h in handles:
			h.inHandle = !h.inHandle
	
	func _init(ms: Shape, inP: Point, outP: Point, t: SegmentType, hand: Array[Handle] = [], selectable = true):
		myShape = ms
		if selectable:
			pass
		# 	select_label = Globl.get_label()
		inPoint = inP
		outPoint = outP
		inPoint.add_adjacent_segment(self)
		outPoint.add_adjacent_segment(self)
		type = t
		if t == SegmentType.CUBIC:
			if len(hand) > 0:
				handles = hand
			else:
				makeCubic()
	
	func pointPositions() -> Array[Vector2]:
		var pps: Array[Vector2] = [inPoint.pos]
		for h: Handle in handles:
			pps.append(h.pos)
		pps.append(outPoint.pos)
		return pps

	func pointPositionsFlat() -> Array[float]:
		var pps: Array[float] = [inPoint.pos[0], inPoint.pos[1]]
		if len(handles) == 2:
			var hInverse: Array[Handle] = [handles[1], handles[0]]
			for h: Handle in hInverse:
				pps.append(h.pos[0])
				pps.append(h.pos[1])
		else:
			pps.append(outPoint.pos[0] + VectorBetweenPoints().normalized()[0])
			pps.append(outPoint.pos[1] + VectorBetweenPoints().normalized()[1])
			pps.append(inPoint.pos[0] - VectorBetweenPoints().normalized()[0])
			pps.append(inPoint.pos[1] - VectorBetweenPoints().normalized()[1])
		pps.append(outPoint.pos[0])
		pps.append(outPoint.pos[1])
		return pps
	
	func getPos() -> Vector2:
		return avPos()

	func setPos(change: Vector2):
		assert(false)
		var allPoints = [inPoint, outPoint]
		for p in allPoints:
			if !p.selected and p not in already_affected_this_frame:
				already_affected_this_frame.append(p)
				p.setPos(change)
		
	func switch_segment_type():
		if type == SegmentType.STRAIGHT:
			makeCubic()
		else:
			makeStraight()
	
	func length():
		return (inPoint.pos - outPoint.pos).length()

	func angle():
		return int(rad_to_deg(((inPoint.pos - outPoint.pos).angle())))*-1 + 180

	func makeStraight():
		if type != SegmentType.STRAIGHT:
			type = SegmentType.STRAIGHT
		# for h in handles:
		# 	h.delete()
		handles = []

	func makeCubic():
		if type != SegmentType.CUBIC:
			type = SegmentType.CUBIC
		var inpPos = inPoint.pos
		var outpPos = outPoint.pos
		var average: Vector2 = (inpPos + outpPos) / 2
		var shapeCenter = myShape.getPos()
		var outpPosCandidate = (average + outpPos) / 2.0 
		var inpPosCandidate = (average + inpPos) / 2.0 
		var inHandle: Handle = Handle.new(outPoint, shapeCenter.direction_to(outpPosCandidate) * 100 + outpPosCandidate)
		inHandle.inHandle = true
		var outHandle: Handle = Handle.new(inPoint, shapeCenter.direction_to(inpPosCandidate) * 100 + inpPosCandidate)
		outHandle.inHandle = false
		handles = [inHandle, outHandle]
		inPoint.align_handles()
		outPoint.align_handles()
	
	func avPos() -> Vector2:
		var outv: Vector2 = Vector2(0,0)
		var pps = pointPositions()
		for p in pps:
			outv += p
		outv /= len(pps)
		return outv
	
	func VectorBetweenPoints() -> Vector2:
		return inPoint.pos -  outPoint.pos

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
	var selected: bool = false
	# var select_label: String = ""
	var inHandle: bool
	var adjacent_point: Point
	var pos: Vector2
	var along_handle_line: float
	# var selection_key: String
	# var next_to_whole_point: bool

	func c() -> String:
		return "Handle"

	func _init(adjacent_p:Point, p=Vector2(0,0), selectable:bool = true, ih = false, ahl = 100):
		adjacent_point = adjacent_p
		pos = p
		inHandle = ih
		along_handle_line = 100
		if selectable:
			pass
			# select_label = Globl.get_label()

	# func delete():
		# Globl.currently_selected_dict.erase(self)
		# Globl.reset_flat_select_dict()
		# Globl.possible_selections_dict.erase(selection_key)

	func getPos() -> Vector2:
		return pos

	func setPos(change: Vector2):
		pos += change

	func dup() -> Handle:
		var h: Handle = Handle.new(adjacent_point, pos, false, inHandle, along_handle_line)
		h.selected = selected
		# h.select_label = select_label
		return h


class Point:
	var ghostPoint: bool = false
	var selected: bool = false
	var select_label: String = ""

	var pos: Vector2
	var type: PointType
	var handle_line: Vector2
	var handle_line2: Vector2
	var auto_handles: bool = true
	var adjacent_segments: Array[Segment] = []
	var subdiv_type: PointSubdiv = PointSubdiv.NONE
	var subdiv_level: Vector2 = Vector2.ZERO

	func c() -> String:
		return "Point"

	func _init(p=Vector2(0,0), t = PointType.BROKEN, selectable: bool = true,
		hl: Vector2 = Vector2.ZERO, autoh: bool = true,
		subt: PointSubdiv = PointSubdiv.NONE, subl: Vector2 = Vector2.ZERO):
		pos = p
		type = t
		if selectable:
			select_label = Globl.get_label()
		handle_line = hl
		handle_line2 = -hl
		auto_handles = autoh
		subdiv_type = subt
		subdiv_level = subl

	func myShape() -> Shape:	
		return adjacent_segments[0].myShape

	func switch_point_type():
		if type == PointType.WHOLE:
			type = PointType.BROKEN
		elif type == PointType.BROKEN:
			type = PointType.WHOLE
			mend()

	func set_handle_line(h: Handle, v: Vector2) -> void:
		if type == PointType.WHOLE:
			if len(adjacent_handles()) == 2:
				if h.inHandle:
					handle_line = v
					handle_line2 = -v
				else:
					handle_line = -v
					handle_line2 = v
			else:
				if h.inHandle:
					handle_line = v
				else:
					handle_line2 = -v
		else:
			if len(adjacent_handles()) == 2:
				if h.inHandle:
					handle_line = v
				else:
					handle_line2 = v
			else:
				if h.inHandle:
					handle_line = v
				else:
					handle_line2 = v

	func subdivide(tpe: PointSubdiv, amount: Vector2):
		subdiv_type = tpe
		subdiv_level = amount

	func get_handle_line(h: Handle) -> Vector2:	
		if h.inHandle:
			return handle_line
		else:
			return handle_line2
		
	func mend():
		var handle_amount = len(adjacent_handles())
		if handle_amount == 0:
			return
		elif handle_amount == 1:
			for seg in adjacent_segments:
				if seg.type == SegmentType.STRAIGHT:
					set_handle_line(adjacent_handles()[0], seg.VectorBetweenPoints().normalized())
			# adjacent_handles()[0].along_handle_line = 400
		elif handle_amount == 2:
			var av_vector: Vector2 = Vector2.ZERO
			for seg in adjacent_segments:
				av_vector += seg.VectorBetweenPoints().normalized()
			set_handle_line(adjacent_handles()[0], av_vector.normalized())
			set_handle_line(adjacent_handles()[1], -av_vector.normalized())
			# handle_line = av_vector.normalized()
			var counter = 0
			for h: Handle in adjacent_handles():
				h.along_handle_line = min(adjacent_segments[counter].VectorBetweenPoints().length()*.3, 30)
				counter += 1
		align_handles()
				
	# func delete():
	# 	Globl.currently_selected_dict.erase(self)
	# 	Globl.reset_flat_select_dict()
	# 	Globl.possible_selections_dict.erase(self)
	
	func add_adjacent_segment(ads: Segment):
		if ads not in adjacent_segments:	
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

	func getPos() -> Vector2:
		return pos

	func setPos(change: Vector2):
		pos += change
		#for h in adjacent_handles():
			#if h not in Globl.currently_selected_dict:
				#h.setPos(change)

	func align_handles():
		if auto_handles:
			auto_adjust_handles()
		for h in adjacent_handles():
			# if type == PointType.BROKEN:
			h.pos = pos + get_handle_line(h) * h.along_handle_line
	
	func auto_adjust_handles():
		var handle_amount = len(adjacent_handles())
		if handle_amount == 0:
			return
		elif handle_amount == 1 and type == PointType.WHOLE:
			for seg in adjacent_segments:
				if seg.type == SegmentType.STRAIGHT:
					set_handle_line(adjacent_handles()[0],seg.VectorBetweenPoints().normalized())
			# adjacent_handles()[0].along_handle_line = 40
		elif handle_amount == 2:
			var av_vector: Vector2 = Vector2.ZERO
			for seg in adjacent_segments:
				av_vector += seg.VectorBetweenPoints().normalized()
			# handle_line = av_vector.normalized()
			set_handle_line(adjacent_handles()[0], av_vector.normalized())
			set_handle_line(adjacent_handles()[1], -av_vector.normalized())
			var counter = 0
			for h: Handle in adjacent_handles():
				h.along_handle_line = adjacent_segments[counter].VectorBetweenPoints().length()*.1
				counter += 1

class Shape:
	var selected: bool = false
	# var select_label: String = ""

	var belongsTo: Shapes = null 
	var points: Array[Point]
	var segments: Array[Segment]
	var closed: bool

	func c() -> String:
		return "Shape"

	func reverse_shape():
		# pass
		points.reverse()
		segments.reverse()
		for s in segments:
			s.reverse_segment()

	func print_segments(seg: Array[Segment]):
		var prStr = "["
		for s: Segment in seg:
			prStr += str(s.inPoint) +"_"+ s.inPoint.select_label + "__" +str(s.outPoint)+"_"+ s.outPoint.select_label + "___"
			for h in s.handles:
				prStr += str(h.pos) + "_" + str(h.inHandle) + "__"
			
			prStr+= "\n"
		prStr += "]"
		print(prStr)

	func _init(p: Array[Point]=[], s: Array[Segment]=[], clo: bool = false, selectable: bool = true):
		points = p
		segments = s
		closed = clo
		if selectable:
			pass
			# select_label = Globl.get_label()


	func auto_adjust_all_handles():
		for p in points:
			if p.auto_handles:
				p.align_handles()

	func add_belongs_too(sps: Shapes):
		belongsTo = sps

	func add_point(m_pos: Vector2, seg: Segment = null, make_selectable: bool = true, make_cubic: bool = false) -> Array:
		var returnList = []
		var p = Point.new(m_pos, PointType.BROKEN, make_selectable)
		returnList.append(p)
		# Globl.add_to_possible_selections(p)
		if len(points) > 0 and points[-1].pos == p.pos:
			points[-1] = p
		else:
			if seg != null:
				points.insert(points.find(seg.outPoint), p)
				var s: Segment
				if make_cubic:
					s = Segment.new(self, p, seg.outPoint, SegmentType.CUBIC)
				else:
					s = Segment.new(self, p, seg.outPoint, SegmentType.STRAIGHT)
				seg.outPoint = p
				p.add_adjacent_segment(s)
				p.add_adjacent_segment(seg)
				
				var where_to_insert
				if segments.find(seg) == len(segments) - 1:
					where_to_insert = 0
				else:
					where_to_insert = segments.find(seg) + 1
				var afterSeg = segments[where_to_insert]
				segments.insert(where_to_insert, s)
				s.outPoint.adjacent_segments = [s,segments[where_to_insert+1]]
				p.switch_point_type()
				for h in seg.handles:
					if h.inHandle:
						h.adjacent_point = p
				for h in afterSeg.handles: 
					if !h.inHandle:
						h.adjacent_point = p
				p.adjacent_segments.reverse()
				returnList.append(s)
				# if len(points) > 1:
				# 	var s: Segment = Segment.new(self, points[-2], p, SegmentType.STRAIGHT)
				# 	segments.append(s)
				# if len(points) > 2:
				# 	points[-2].switch_point_type()
				# 	# Globl.add_to_possible_selections(s)
			else:
				points.append(p)
				if len(points) > 1:
					var s: Segment = Segment.new(self, points[-2], p, SegmentType.STRAIGHT)
					segments.append(s)
				if len(points) > 2:
					points[-2].switch_point_type()
					# Globl.add_to_possible_selections(s)
		return returnList


	func delete():
		if belongsTo:
			belongsTo.remove_shape(self)
		for seg in segments:
			seg.inPoint = null
			seg.outPoint = null
			seg.myShape = null
			for h in seg.handles:
				h.adjacent_point = null
			seg.handles = []
		for p in points:
			p.adjacent_segments = []
		segments = []
		points = []

	func make_clockwise():
		if !clockwise():
			reverse_shape()

	func close():
		closed = true	
		if len(points) < 3:
			delete()
		else:
			var s: Segment = Segment.new(self, points[-1], points[0], SegmentType.STRAIGHT)
			points[0].switch_point_type()
			points[-1].switch_point_type()
			segments.append(s)
			points[0].adjacent_segments.reverse()
		make_clockwise()
				# p.align_handles()
			# Globl.add_to_possible_selections(s)
	
	func getPos() -> Vector2:
		var aver: Vector2 = Vector2(0,0)
		for p in points:
			aver += p.pos
		aver /= len(points)
		return aver

	func clockwise() -> bool:
		var lowesty = 10000000
		var lowestPoint: Point = null
		for p in points:
			if p.pos.y < lowesty:
				lowesty = p.pos.y
				lowestPoint = p
		var A: Vector2 = lowestPoint.pos
		var Aplace = points.find(lowestPoint)
		var B: Vector2 = points[Aplace-1].pos
		var C: Vector2
		if Aplace == len(points) -1:
			C = points[0].pos
		else:
			C = points[Aplace+1].pos
		return (B-A).cross(C-A) <= 0
		
	func setPos(change: Vector2):
		assert(false)

	func shape_string() -> String:
		var shape_str: String = ""
		for s in segments:
			if shape_str.length() == 0:
				shape_str += "M {0} {1} ".format([s.inPoint.pos.x, s.inPoint.pos.y])
			shape_str += s.toSVG()
		if closed:
			shape_str += 'Z\n'
		return shape_str

	func dup() -> Shape:
		var points_copy: Array[Point] = []
		var points_copy_dict: Dictionary = {}
		for p in points:
			var point_copy = Point.new(p.pos, p.type, false, p.handle_line, p.auto_handles, p.subdiv_type, p.subdiv_level)
			point_copy.select_label = p.select_label
			point_copy.selected = p.selected
			point_copy.adjacent_segments.clear()
			points_copy.append(point_copy)
			points_copy_dict[p] = point_copy
		
		var segments_copy: Array[Segment] = []
		var segAm = 0
		for seg in segments:
			segAm += 1
			var nInp: Point = points_copy_dict[seg.inPoint]
			var nOutp: Point = points_copy_dict[seg.outPoint]
			var nHand: Array[Handle] = []
			for h in seg.handles:
				nHand.append(h.dup())
			var seg_copy = Segment.new(self, nInp, nOutp,seg.type,nHand, false)
			# seg_copy.select_label = seg.select_label
			seg_copy.selected = seg.selected	
			nInp.add_adjacent_segment(seg_copy)
			nOutp.add_adjacent_segment(seg_copy)
			for h in nInp.adjacent_handles():
				h.adjacent_point = nInp
			for h in nOutp.adjacent_handles():
				h.adjacent_point = nOutp
			segments_copy.append(seg_copy)

		var sp: Shape = Shape.new(points_copy, segments_copy, closed, false)
		for seg in sp.segments:
			seg.myShape = sp
		# sp.select_label = select_label
		return sp


class Shapes:
	var shapes: Array[Shape] = []

	func delete():
		for s in shapes:
			s.delete()
		shapes = []
	
	func c() -> String:
		return "Shapes"

	func add_shape(s: Shape):
		var to_append: Shape = Shape.new(s.points, s.segments, s.closed) 	
		if len(to_append.points) > 2:
			to_append.add_belongs_too(self)
			to_append.close()
			shapes.append(to_append)
			# Globl.add_to_possible_selections(to_append)
	
	func remove_shape(s: Shape):
		shapes.erase(s)

	func dup() -> Shapes:
		var sps = Shapes.new()
		for sp: Shape in shapes:
			sp.add_belongs_too(sps)
			sps.shapes.append(sp.dup())
		return sps
	
	func set_selection_dicts():
		var selectable_dict = {}
		var selected_dict = {}
		for s in shapes:
			# selectable_dict[s.select_label] = s
			if s.selected:
				selected_dict[s] = true
			for seg in s.segments:
				# selectable_dict[seg.select_label] = seg
				if seg.inPoint.selected and seg.outPoint.selected:
					selected_dict[seg] = true
				selectable_dict[seg.inPoint.select_label] = seg.inPoint
				if seg.inPoint.selected:
					selected_dict[seg.inPoint] = true
				selectable_dict[seg.outPoint.select_label] = seg.outPoint
				if seg.outPoint.selected:
					selected_dict[seg.outPoint] = true
				for h in seg.handles:
					if h.selected:
						selected_dict[h] = true
		Globl.possible_selections_dict = selectable_dict
		Globl.currently_selected_dict = selected_dict
		Globl.reset_flat_select_dict()

# Called when the node enters the scene tree for the first time.
const borders: Vector2 = Vector2(0,0)
const far_move_border: int = 2 * grid_size
const grid_size: int = 16
var grid_modifier: float = 8
var back_color: Color = Color.from_ok_hsl(43/359.0, 45/100.0, 70/100.0)
var back_selecting_color: Color = Color.from_ok_hsl(63/359.0, 9/100.0, 66/100.0)
# var back_color_preview: Color = Color.from_ok_hsl(261/359.0, 73/100.0, 30/100.0)
var back_color_preview: Color = Color.from_rgba8(240,220,200)
# var back_color_preview: Color = Color.from_ok_hsl(264/359.0, 100/100.0, 47/100.0)
var ManagerScript 
var manager_node
var imageArray: Array = []
var textureArray: Array = []
var im: Image
# var im_magnified: Image
var tex: Sprite2D
# var tex_magnified: Sprite2D
var counter: float
var window_size: Vector2 = Vector2(96,112) * grid_size
var marker_pos: Vector2;
var origin: Vector2
var shapes: Shapes = Shapes.new()
var shape: Shape
var old_window_size: Vector2
var keys_pressed_array: Array[String] = []
var zoom: float = 1

var preview: bool = false
var select_mode: bool = false
var show_point_select: bool = true
var show_handle_select: bool = true
var show_line_select: bool = true
var show_shape_select: bool = true
# var possible_selections_dict: Dictionary = {}
# var currently_selected: Array = []

var held_time: Dictionary = {"left" : 0, "right" : 0, "up" : 0, "down" : 0}
var held_time2: float = 0
var valid_hold_length = .2
var light_font
var medium_font
var italic_font
var bold_font
var default_font_size 
var current_grid_visual_size
var undo_timer = Timer.new()
var can_undo_again: bool = true
var movement_timer = Timer.new()
var can_move_again: bool = true
var sticky_border_bool: bool = true
var a = 1.00
var highlight_j: bool = false
var highlight_k: bool = false
var deltaTimeArray: Array = []

# var currently_selected_dict: Dictionary = {}
# var currently_selected_flat: Dictionary = {}
# var possible_selections_dict: Dictionary = {}

func _ready() -> void:
	add_child(undo_timer)
	add_child(movement_timer)
	movement_timer.wait_time = 0.01
	movement_timer.one_shot = true
	movement_timer.timeout.connect(mt_timeout)
	undo_timer.wait_time = .1
	undo_timer.one_shot = true
	undo_timer.timeout.connect(do_undo_redo)
	ManagerScript = load("res://Manager.cs")
	manager_node = ManagerScript.new() 
	add_child(manager_node)
		
	light_font = load("res://assets/DraftingMono/DraftingMono-Light.otf")
	medium_font = load("res://assets/DraftingMono/DraftingMono-Medium.otf")
	italic_font = load("res://assets/DraftingMono/DraftingMono-LightItalic.otf")
	bold_font = load("res://assets/DraftingMono/DraftingMono-Bold.otf")
	# default_font = ThemeDB.fallback_font
	default_font_size = 15
	get_window().size = window_size
	marker_pos = window_size / 2
	origin = marker_pos
	for i in range(30):
		var imm: Image = Image.new() 
		imageArray.append(imm)
		var texx: Sprite2D = Sprite2D.new()
		texx.position = Vector2(0,0)
		add_child(texx)
		textureArray.append(texx)
	
	im = Image.new()
	# im_magnified = Image.new()
	tex = $Tex
	# tex_magnified = $Magnified
	current_grid_visual_size = grid_modifier * grid_size * zoom
	shape = Shape.new()
	# for i in range(2):
	# 	create_random_shape()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	# if (len(shapes.shapes)>1):
	# 	print("\n\n")
	# 	print(merge_flat_shapes(flattenShape(shapes.shapes[0]), flattenShape(shapes.shapes[1])))

	if len(deltaTimeArray) > 60:
		deltaTimeArray.pop_front()
	deltaTimeArray.append(delta)
	var totalDelta = 0
	var totPoints = 0
	for t in deltaTimeArray:
		totalDelta += t
	for s in shapes.shapes:
		totPoints += len(s.points)
	$fps.text = str(roundi(1.0 / (totalDelta / len(deltaTimeArray)))) + " : " + str(totPoints)
	# a = clamp(a- (2.1 - a)*delta, 0,1)
	# if select_mode:
	# 	a = 1
	shapes.set_selection_dicts()
	if zoom <= 1:
		$Cursor.position = lerp($Cursor.position, (marker_pos * zoom)+ (origin *(1 -  zoom)), delta * 20)
	else:
		$Cursor.position = origin
	$Cursor.scale = lerp($Cursor.scale, Vector2(.5,.5), delta * 10)
	$Cursor.rotation = $Cursor.position.angle_to_point((marker_pos * zoom) + (origin * (1-zoom)))
	$Cursor/Shad.rotation = -$Cursor.rotation
	$Cursor/Shad.offset = Vector2(6,10)
	# $Cursor.scale = Vector2(.5,clamp(.5 - ($Cursor.position.distance_to(marker_pos) / 100),.40,.5))
	Segment.already_affected_this_frame = []
	# $Magnified.visible = false
	counter += delta
	var radius: float = 40 * sin(counter)

	if Input.is_action_just_pressed("toggle_preview"):
		preview = !preview
	# if Input.is_action_just_pressed("toggle_point_select"):
	# 	show_point_select = !show_point_select
	# if Input.is_action_just_pressed("toggle_handle_select"):
	# 	show_handle_select = !show_handle_select
	# if Input.is_action_just_pressed("toggle_line_select"):
	# 	show_line_select = !show_line_select
	# if Input.is_action_just_pressed("toggle_shape_select"):
	# 	show_shape_select = !show_shape_select
	if Input.is_key_pressed(KEY_BACKSPACE):
		get_tree().quit()
	if Input.is_action_just_pressed("select_mode"):
		if !select_mode:
			select_mode = true
			$Background.color = back_selecting_color
			keys_pressed_array = []
		else:
			select_mode = false
			keys_pressed_array= []
	# if Input.is_action_just_released("select_mode"):
	# 	pass
	# if Input.is_action_just_released("select_mode"):
	var back: ColorRect = $Background
	if preview:
		back.color = back_color_preview
	elif !select_mode:
		back.color = back_color

	if !select_mode:
		handle_input(delta)

	var sw: float = 2
	var visible_point_size: int = 30
	if zoom > 1:
		visible_point_size = 8
		sw = 1

	var last_point_placed: String = ""
	if len(shape.points) > 0:
		last_point_placed += '<circle cx="{0}" cy="{1}" r="{2}" stroke="blue" fill-opacity=".0" stroke-width="{3}"/>'.format([shape.points[-1].pos.x, shape.points[-1].pos.y, visible_point_size-2, sw * .5])

	var shape_closed_look: String = '
		stroke="black"
		fill="gray"
		# stroke-width="'+str(sw+1)+'"
		stroke-width="2.0"
		stroke-opacity="0.5"
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
			fill="black"
			stroke-width="0"
			fill-opacity="2"
		'
		shape_closed_look = shape_look
		shape_open_look = shape_look
	var tslating = origin	
	if zoom > 1:
		tslating = origin + 1.333*(marker_pos - origin)
	var svg_to_draw: String = (
# svg 
'<svg xmlns="http://www.w3.org/2000/svg" width="{0}" height="{1}">'.format([window_size.x,window_size.y]) + 
'<g transform="scale({0}) translate({1},{2}) rotate({3})">'.format([1,tslating.x,tslating.y,0]) + 
'<g transform="scale({0}) translate({1},{2}) rotate({3})">'.format([zoom,-tslating.x,-tslating.y,0])) 
	if !preview and !select_mode:
		var guideLinesH: int = 24
		var guideLinesV: int = 24
		if zoom < 1:
			svg_to_draw += (
			'
			<rect x="{0}" y="{1}" width="{2}" height="{3}" fill-opacity="0.0" stroke-opacity=".1" stroke="black" stroke-width="20"/>
			'.format([origin.x - guideLinesV*grid_size*2, origin.x - guideLinesV*grid_size*2,8*grid_size*12,8*grid_size*14])
			)
		var guid_op = .05
		svg_to_draw += (
		'
		<path d="M {0} 0 V {1}" stroke="black" stroke-opacity={2} stroke-width="2"/>
		'.format([origin.x - guideLinesV*grid_size, 8 * grid_size * 14, guid_op])
		+ 
		'
		<path d="M {0} 0 V {1}" stroke="black" stroke-opacity={2} stroke-width="2"/>
		'.format([origin.x + guideLinesV*grid_size, 8 * grid_size * 14, guid_op])
		+ 
		'
		<path d="M 0 {0} H {1}" stroke="black" stroke-opacity={2} stroke-width="2"/>
		'.format([origin.y - guideLinesH*grid_size,  8*grid_size*12, guid_op])
		+ 
		'
		<path d="M 0 {0} H {1}" stroke="black" stroke-opacity="{2}" stroke-width="2"/>
		'.format([origin.y + guideLinesH*grid_size, 8*grid_size*12, guid_op])
		 +
		'
		<path d="M 0 {0} H {1}" stroke="black" stroke-opacity={2} stroke-width="2"/>
		'.format([origin.y - guideLinesH*2*grid_size, 8*grid_size*12, guid_op])
		+ 
		'
		<path d="M 0 {0} H {1}" stroke="black" stroke-opacity="{2}" stroke-width="2"/>
		'.format([origin.y + guideLinesH*2*grid_size,8 * grid_size*12, guid_op])
		)
	var marker_min_dist_to_points = 10000
	var ghostList = []
	var ghostsegs = []
	# for s in shapes.shapes:
	# 	svg_to_draw += (
	# 		s.shape_string()
	# 	)
	if false:
		for s in shapes.shapes:
			svg_to_draw += (
				s.shape_string()
			)
	else:
		# for realShape in shapes.shapes:
		if len(shapes.shapes) > 1:
			var spes = merge_flat_shapes(flattenShape(shapes.shapes[0]), flattenShape(shapes.shapes[1]))
			print("\n\nlenspes:" + str(len(spes)))
			svg_to_draw += (
			'<path fill-rule="nonzero" d="'
			)
			for spe in spes:
				svg_to_draw += (
					flatShapeToString(create_ghost_shape_flat(spe))
					# flatShapeToString(spe)
					# flatShapeToString(merge_flat_shapes(flattenShape(shapes.shapes[0]), flattenShape(shapes.shapes[1])))
				)
			svg_to_draw += (
				'"' + shape_closed_look + 
				'/>'
			)
		# print(svg_to_draw)
		if !preview:
			for spe in shapes.shapes:
				var gsf = create_ghost_shape_flat(flattenShape(spe))
				svg_to_draw += (
				'<path d="'
				)
				svg_to_draw += (
					flatShapeToString(gsf)
					# flatShapeToString(merge_flat_shapes(flattenShape(shapes.shapes[0]), flattenShape(shapes.shapes[1])))
				)

				svg_to_draw += (
					'"' + shape_closed_look + '/>'
				)
				for fs: FlatSegment in gsf:
					var diff = fs.inPoint - fs.outPoint
					var inpos = fs.inPoint - 0.01*diff
					var outpos = fs.outPoint + 0.01*diff
					var inhos = fs.inHandle
					var outhos = fs.outHandle
					svg_to_draw += (
						'<circle cx="' + str(inpos.x) + '" cy="' + str(inpos.y) + '" r="' + str(visible_point_size / 2.) + '" fill-opacity="0.1" fill="yellow" stroke="yellow" stroke-opacity="0.3" stroke-width="'+str(sw+3)+'"/>'
					)
					svg_to_draw += (
						'<circle cx="' + str(outpos.x) + '" cy="' + str(outpos.y) + '" r="' + str(visible_point_size / 2.) + '" fill-opacity="0.1" fill="blue" stroke="blue" stroke-opacity="0.3" stroke-width="'+str(sw+3)+'"/>'
					)
					svg_to_draw += (
						'<circle cx="' + str(inhos.x) + '" cy="' + str(inhos.y) + '" r="' + str(visible_point_size / 3.) + '" fill-opacity="0.2" fill="yellow" stroke="yellow" stroke-opacity="0.3" stroke-width="'+str(sw-1)+'"/>'
					)
					svg_to_draw += (
						'<circle cx="' + str(outhos.x) + '" cy="' + str(outhos.y) + '" r="' + str(visible_point_size / 3.) + '" fill-opacity="0.2" fill="blue" stroke="blue" stroke-opacity="0.3" stroke-width="'+str(sw-1)+'"/>'
					)


	for s in shapes.shapes:
		if !preview:
			for p in s.points:
				var dist = marker_pos.distance_to(p.getPos())
				if dist < marker_min_dist_to_points:
					marker_min_dist_to_points = dist
				# if show_shape_select:
					# svg_to_draw += (
					# 	'<path d="M {0} {1} L {2} {3}" stroke="white" stroke-opacity=".30" stroke-dasharray="20,20" stroke-width="2.0"/>'.format([str(s.getPos().x), str(s.getPos().y), str(p.pos.x), str(p.pos.y)])
					# )

				if p.selected:
					svg_to_draw += (
						'<circle cx="' + str(p.pos.x) + '" cy="' + str(p.pos.y) + '" r="' + str(visible_point_size / 2.) + '" fill-opacity="0.2" fill="red" stroke="black" stroke-opacity="0.5" stroke-width="'+str(sw+1)+'" stroke="black"/>'
					)
				if p.type == PointType.WHOLE:
					pass
				elif p.type == PointType.BROKEN:
					pass
					# svg_to_draw += (
					# 	'<circle cx="' + str(p.pos.x) + '" cy="' + str(p.pos.y) + '" r="' + str(visible_point_size / 3.) + '" fill-opacity="0.1" fill="white" stroke="white" stroke-opacity="0.2" stroke-width="'+str(sw+1)+'" stroke="black"/>'
					# )
					# svg_to_draw += (
					# 	'<polygon points="' + p1 + ' ' +  p2 + ' ' + p3 + '" fill-opacity="0.0" stroke-opacity="0.5" stroke-width="'+str(sw+1)+'" stroke="black"/>'
					# )
				if show_handle_select:
					var inHCol = "black"
					var outHCol = "black"
					for h in p.adjacent_handles(): 
						var vn = (h.pos - p.pos).normalized()
						if h.along_handle_line < 0:
							vn = -(h.pos - p.pos).normalized()
						var p1 = h.pos - vn.rotated(-1)*-15
						var p2 = h.pos - vn*(-20)
						var p3 = h.pos - vn.rotated(1)*-15
						
						svg_to_draw += (
							'<path d="M {0} {1} L {2} {3} L {4} {5}" stroke="black" fill-opacity="0.0" stroke-opacity=".2" stroke-width="2.0"/>'.format([str(p1.x), str(p1.y), str(p2.x), str(p2.y), str(p3.x), str(p3.y)]) 
						)


						var opa = .3
						var opaf = 0.0
						var ring_thickness = sw + 2
						if h.selected:
							opa = 1.0
							opaf = 0.5
							ring_thickness = sw
						var dotline_opa = .3
						var dotline_width = 3.0
						var stroke_string = 'stroke-dasharray="7,5"' 
						if p.type == PointType.WHOLE:
							dotline_opa = .4
							dotline_width = 3.0
							stroke_string = '' 
						if true:
							inHCol = "blue"
							outHCol = "red"
							if h.inHandle:		
								# if highlight_k:
								if true:
									svg_to_draw += (
										'<path d="M {0} {1} L {2} {3}" stroke="black" stroke-opacity="{4}" {5} stroke-width="{6}"/>'.format([str(h.pos.x), str(h.pos.y), str(p.pos.x), str(p.pos.y), str(dotline_opa), stroke_string, str(dotline_width)]) + 
										'<circle cx="' + str(h.pos.x) + '" cy="' + str(h.pos.y) + '" r="' + str(visible_point_size / 3.) + '" fill-opacity="'+ str(opaf)+'" stroke-opacity="'+ str(opa) +'" stroke-width="'+str(ring_thickness+3)+'" fill="blue" stroke="'+inHCol+'"/>'
									)
								else:
									svg_to_draw += (
										'<path d="M {0} {1} L {2} {3}" stroke="black" stroke-opacity="{4}" {5} stroke-width="{6}"/>'.format([str(h.pos.x), str(h.pos.y), str(p.pos.x), str(p.pos.y), str(dotline_opa), stroke_string, str(dotline_width)]) + 
										'<circle cx="' + str(h.pos.x) + '" cy="' + str(h.pos.y) + '" r="' + str(visible_point_size / 4.) + '" fill-opacity="'+ str(opaf)+'" stroke-opacity="'+ str(opa) +'" stroke-width="'+str(ring_thickness)+'" fill="blue" stroke="'+inHCol+'"/>'
									)
							else:
								if true:
								# if highlight_j:
									svg_to_draw += (
										'<path d="M {0} {1} L {2} {3}" stroke="black" stroke-opacity="{4}" {5} stroke-width="{6}"/>'.format([str(h.pos.x), str(h.pos.y), str(p.pos.x), str(p.pos.y), str(dotline_opa), stroke_string, str(dotline_width)]) + 
										'<circle cx="' + str(h.pos.x) + '" cy="' + str(h.pos.y) + '" r="' + str(visible_point_size / 3.) + '" fill-opacity="'+str(opaf)+'" stroke-opacity="' + str(opa) + '" stroke-width="'+str(ring_thickness+3)+'" fill="red" stroke="'+outHCol+'"/>'
									)
								else:
									svg_to_draw += (
										'<path d="M {0} {1} L {2} {3}" stroke="black" stroke-opacity="{4}" {5} stroke-width="{6}"/>'.format([str(h.pos.x), str(h.pos.y), str(p.pos.x), str(p.pos.y), str(dotline_opa), stroke_string, str(dotline_width)]) + 
										'<circle cx="' + str(h.pos.x) + '" cy="' + str(h.pos.y) + '" r="' + str(visible_point_size / 4.) + '" fill-opacity="'+str(opaf)+'" stroke-opacity="' + str(opa) + '" stroke-width="'+str(ring_thickness)+'" fill="red" stroke="'+outHCol+'"/>'
									)

	svg_to_draw += (
	'<path
		d="' + shape.shape_string() + '"' + 
		shape_open_look
		+ '/>'
	)
	svg_to_draw += last_point_placed
	svg_to_draw += '</g></g></svg>'
		
	im.load_svg_from_string(svg_to_draw)
	tex.texture = ImageTexture.create_from_image(im)
	
	if on_border() and sticky_border_bool:
		movement_timer.wait_time = .2
		movement_timer.start()
		can_move_again = false
		sticky_border_bool = false
	# elif marker_min_dist_to_points < grid_modifier*grid_size*2:
	# 	movement_timer.wait_time = .1
	else:
		movement_timer.wait_time = .01
	queue_redraw()

	
func _draw() -> void:
	if preview:
		return
	var vp: Vector2 = get_window().size
	var al = .04
	if grid_modifier >= .5 and !select_mode:	
		current_grid_visual_size = lerp(current_grid_visual_size, zoom*grid_modifier*grid_size, 0.3)

			# p = (p * zoom)+ (origin *(1 -  zoom))
		# if abs(current_grid_visual_size - (zoom*grid_modifier*grid_size)) > .1 * current_grid_visual_size:
		# 	al = .15
		var modif = Vector2(0,0)
		if zoom > 1:
			modif = Vector2(256,128)
		for i in range((vp.y / (grid_size * grid_modifier) + 30) * (1/ zoom)):
			draw_line(Vector2(0,i * current_grid_visual_size - modif.y),Vector2(3000,i * current_grid_visual_size - modif.y),Color.from_rgba8(0,0,0,al*255),1.5,true);
		for i in range((vp.x / (grid_size * grid_modifier) + 30) * (1 / zoom)):
			draw_line(Vector2(i * current_grid_visual_size - modif.x,0),Vector2(i*current_grid_visual_size - modif.x,3000),Color.from_rgba8(0,0,0,al*255),1.5,true);
	
	# draw_circle(marker_pos, 20, Color.from_rgba8(200,0,0,150),false, 1.5,true)
	# draw_string(default_font, marker_pos, str(marker_pos - origin), HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size)
	if zoom <= 1:
		# draw_string(light_font, (marker_pos * zoom)+ (origin *(1 -  zoom)) + Vector2(30,30), str(Vector2i((marker_pos-origin)/grid_size * 10)), HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size + 8)
		draw_string(light_font, (marker_pos * zoom)+ (origin *(1 -  zoom)) + Vector2(30,30), str(marker_pos), HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size + 8)
	else:
		# draw_string(light_font, origin + Vector2(30,30), str(Vector2i((marker_pos-origin)/grid_size * 10)), HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size + 8)
		draw_string(light_font, origin + Vector2(30,30), str(marker_pos), HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size + 8)

	# for selectable in Globl.currently_selected:
	# 	draw_circle(selectable.getPos() - Vector2i(-1,0), 10, Color.from_rgba8(250,250,250,150),true, -1.0,true)
	for s: Shape in shapes.shapes:
		for seg: Segment in s.segments:

			if len(seg.handles) == 2 and (seg.inPoint.selected or seg.outPoint.selected):
				var pps = seg.pointPositionsFlat()
				var where_along: Array = $Player.point_along_cubic_parametric(.5,pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])
				var tang: Array = $Player.tangent_parametric(.5,pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])
				var tang_vector = Vector2(tang[0], tang[1])
				# var normal_vector = tang_vector.rotated(.5 * PI)
				var wa_vec: Vector2 = Vector2(where_along[0], where_along[1])
				# draw_circle(wa_vec, 7, Color.from_rgba8(50,50,50,150),true, -1.0,true)
				# draw_line(wa_vec, wa_vec + tang_vector*100, Color.BLACK, 2, true)
				# var arrow_col: Color = Color.from_rgba8(0,0,0,50)
				# draw_line(wa_vec + normal_vector*8, wa_vec + (Vector2(tang[0], tang[1]))*8, arrow_col, 2, true)
				# draw_line(wa_vec + normal_vector*-8, wa_vec + (Vector2(tang[0], tang[1]))*8, arrow_col, 2, true)
				var tv = tang_vector.angle()
				if abs(tv) > .5 * PI:
					tv -= PI
				draw_set_transform(wa_vec, tv)
				draw_string(light_font, Vector2(5,-5), str(seg.angle()) + "°", HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size+2)
				draw_set_transform(Vector2(0,0),0)
					# draw_string(light_font, (wa_vec * zoom)+ (origin *(1 -  zoom)) + Vector2(5,25), str(int(seg.length()/10)) + " mm", HORIZONTAL_ALIGNMENT_LEFT, -1, default_font_size + 8)
			
	if true:
		for sel_text in Globl.possible_selections_dict:
			# var a = clamp(((3*sin(2*counter) + 3) / 3.0),0,1.0)
			var font_size_to_use = default_font_size + 12
			var a = 1.0
			# if a <= 0:
			# 	continue
			var col: Color = Color.WHITE
			var selectable = Globl.possible_selections_dict[sel_text]
			if selectable.selected and !select_mode:
				continue
			var font_to_use = medium_font
			var off = Vector2(0,0)
			if selectable.c() == "Point":
				if !show_point_select:
					continue
				off = Vector2(-13,13)
			if selectable.c() == "Handle":
				if !show_handle_select:
					continue
				font_to_use = light_font
			if selectable.c() == "Segment":
				if !show_line_select:
					continue
				font_to_use = italic_font
				font_size_to_use += 2
			if selectable.c() == "Shape":
				if !show_shape_select:
					continue
				font_to_use = bold_font
				font_size_to_use += 4
				# font_size_to_use = default_font_size + 14

			# var p: Vector2i = selectable.getPos()
			var p: Vector2 = selectable.getPos()
			if zoom <= 1:
				p = (p * zoom)+ (origin *(1 -  zoom))
			else:
				p -= origin - 1.333*(origin - marker_pos)
				p = Vector2(p.x * zoom, p.y * zoom)
				p += origin - 1.333*(origin - marker_pos)
				p -= Vector2(4,-5)
			if !select_mode:
				font_size_to_use -= 3
				a = .9
			col = Color.from_rgba8(80,80,80,a * 100)
			var offVector: Vector2 = 10*Vector2(cos(counter*2+hash(sel_text)), sin(counter*2 + hash(sel_text))) - Vector2(10,-10)
			draw_string_outline(font_to_use, (p - offVector) + off, sel_text,HORIZONTAL_ALIGNMENT_CENTER,-1, 20, font_size_to_use,col)
			col = Color.from_rgba8(255,255,255,a * 255)
			draw_string(font_to_use, (p - offVector) + off, sel_text,HORIZONTAL_ALIGNMENT_CENTER, -1, font_size_to_use, col)

			# var s = clamp((1+sin(counter))/2.0,0.001,0.999)
		# for selectable in Globl.currently_selected_dict:
		# 	if selectable.c() == "Segment" and len(selectable.handles) > 0:
		# 		var s = clamp((1+sin(counter))/2.0,0.001,0.999)
		# 		var pps = selectable.pointPositionsFlat()
		# 		var where_along: Array = $Player.point_along_cubic_parametric(s,pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])
		# 		var tang: Array = $Player.tangent_parametric(s,pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])
		# 		var wa_vec: Vector2 = Vector2(where_along[0], where_along[1])
		# 		draw_circle(wa_vec, 7, Color.from_rgba8(50,50,50,150),true, -1.0,true)
		# 		draw_line(wa_vec, wa_vec + (Vector2(tang[0], tang[1]))*100, Color.BLACK, 2, true)
			# var p: Vector2 = selectable.getPos()
			# p = (p * zoom)+ (origin *(1 -  zoom))
			# draw_circle(p, 7, Color.from_rgba8(50,50,50,150),true, -1.0,true)


func check_borders(mpos: Vector2) -> Vector2:
	var new_mpos: Vector2 = mpos
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
			highlight_j = false
			highlight_k = false
			# if event.as_text() == 'Shift+Space' or event.as_text() == 'Space':
			# 	handle_selection_text(keys_pressed_array.duplicate())
			# 	keys_pressed_array= []
			if select_mode:
				if !event.as_text() =='Shift+Space' and !event.as_text() == 'Space' and !event.as_text() == 'Semicolon':
					if event.as_text().length() > 0:
						if event.as_text().begins_with('Shift'):
							if event.as_text().length() > 6:
								keys_pressed_array.append(event.as_text()[6])
								handle_selection_text(keys_pressed_array.duplicate())
								keys_pressed_array = []
						else:
							if event.as_text().to_lower() == 'j' or event.as_text().to_lower() == 'k':
								if event.as_text().to_lower() == 'j':
									highlight_j = true
								if event.as_text().to_lower() == 'k':
									highlight_k = true
								keys_pressed_array.append(event.as_text()[0].to_lower())
							else:
								keys_pressed_array.append(event.as_text()[0].to_lower())
								handle_selection_text(keys_pressed_array.duplicate())
								keys_pressed_array = []
							# keys_pressed_array.append(event.as_text()[0])
				# if len(keys_pressed_array) > 1:
				# 	handle_selection_text(keys_pressed_array.duplicate())
				# 	keys_pressed_array = []
				# elif event.as_text() == 'Semicolon':
				# 		select_mode = false
					
			# elif event.as_text() == 'Semicolon':
			# 	select_mode = true
			# 	keys_pressed_array = []

			if event.as_text() == 'Semicolon':
				for sel in Globl.currently_selected_dict:
					sel.selected = false
				keys_pressed_array = []


func handle_selection_text(kpa: Array[String]):
	if kpa[0] == 'j' or kpa[0] == 'k':
		var s = kpa[1]	
		if s in Globl.possible_selections_dict:
			var possible_handles: Array[Handle] = Globl.possible_selections_dict[s].adjacent_handles()
			var sel: Handle
			if len(possible_handles) == 0:
				return
			elif len(possible_handles) == 1:
				sel = possible_handles[0]
			else:
				if kpa[0] == 'j':
					sel = possible_handles[1]
				else:
					sel = possible_handles[0]
			if sel in Globl.currently_selected_dict:
				sel.selected = false
			else:
				sel.selected = true
	else:
		var s = kpa[0]	
		if s in Globl.possible_selections_dict:
			var sel = Globl.possible_selections_dict[s]
			if sel in Globl.currently_selected_dict:
				sel.selected = false
			else:
				sel.selected = true

func handle_input(delta: float):
	if !select_mode:
		hi_movement(delta)
		hi_rotation(delta)
		hi_scaling(delta)
		hi_skewing(delta)
		hi_point_adding(delta)
		hi_undoing(delta)

func hi_undoing(delta: float):
	if Input.is_action_just_pressed("undo"):
		$UndoRedo.undo()
	if Input.is_action_just_pressed("redo"):
		$UndoRedo.redo()

func hi_scaling(delta: float):
	var scaling_amount = .01
	var x_scaling_amount = scaling_amount
	var y_scaling_amount = scaling_amount
	var displacing = false
	var x_scalar = 1
	var y_scalar = 1
	if Input.is_key_pressed(KEY_5) or Input.is_key_pressed(KEY_6):
		if Input.is_key_pressed(KEY_5):
			x_scaling_amount *= -1
		x_scalar += x_scaling_amount
		displacing = true
	if Input.is_key_pressed(KEY_3) or Input.is_key_pressed(KEY_4):
		if Input.is_key_pressed(KEY_3):
			y_scaling_amount *= -1
		y_scalar += y_scaling_amount
		displacing = true
	if displacing:
		for sel in Globl.currently_selected_flat:
			var spot: Vector2 = sel.getPos() - marker_pos
			var displacement = Vector2(spot[0] * x_scalar, spot[1] * y_scalar)
			sel.setPos(Vector2(displacement-(spot)))

func hi_rotation(delta: float):
	var rotation_amount = .05 
	var ang = rotation_amount * 2 * PI * delta
	if Input.is_key_pressed(KEY_1) or Input.is_key_pressed(KEY_2):
		if Input.is_key_pressed(KEY_2):
			ang *= -1
		var displacement: Vector2
		for sel in Globl.currently_selected_flat:
			var spot: Vector2 = sel.getPos() - marker_pos
			displacement = Vector2(cos(ang)*spot[0] - sin(ang)*spot[1], sin(ang)*spot[0] + cos(ang)*spot[1])
			sel.setPos(Vector2(displacement-(spot)))

func hi_skewing(delta: float):
	var rotation_amount = .0005 
	var ang = rotation_amount * 2 * PI
	if Input.is_key_pressed(KEY_1) or Input.is_key_pressed(KEY_2):
		if Input.is_key_pressed(KEY_2):
			ang *= -1
		var displacement: Vector2
		for sel in Globl.currently_selected_flat:
			var spot: Vector2 = sel.getPos() - marker_pos
			displacement = Vector2(cos(ang)*spot[0] - sin(ang)*spot[1], sin(ang)*spot[0] + cos(ang)*spot[1])
			sel.setPos(Vector2(displacement-(spot)))

func hi_movement(delta: float):
	var movement_amount: int = grid_size * grid_modifier
	if movement_timer.is_stopped():
		movement_timer.start()
	var moving_selected: Vector2 = Vector2(0,0)
	# if Input.is_key_pressed(KEY_SHIFT):
	# 	movement_amount = grid_size * 12

	if Input.is_key_pressed(KEY_A):
		movement_amount = 1
		# $Magnified.visible = true

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
		if held_time2 > valid_hold_length and can_move_again:
			moving_selected.x -= movement_amount
		else:
			held_time2 += delta
	if Input.is_action_pressed("ui_right"): 
		if held_time2 > valid_hold_length and can_move_again:
			moving_selected.x += movement_amount
		else:
			held_time2 += delta
	if Input.is_action_pressed("ui_up"): 
		if held_time2 > valid_hold_length and can_move_again:
			moving_selected.y -= movement_amount
		else:
			held_time2 += delta
	if Input.is_action_pressed("ui_down"): 
		if held_time2 > valid_hold_length and can_move_again:
			moving_selected.y += movement_amount
		else:
			held_time2 += delta
	if moving_selected != Vector2(0,0):
		can_move_again = false
		if not on_border():
			sticky_border_bool = true
		uts()
		var mended_point_list = []
		var auto_adjusted_list = []
		var ms: Shape
		if len(Globl.currently_selected_flat) > 0:
			for sel in Globl.currently_selected_flat:
				sel.pos += moving_selected
				# sel.setPos(moving_selected)
				# if sel.c() == "Handle" and sel.adjacent_point.type == PointType.WHOLE and sel.adjacent_point not in Globl.currently_selected_flat:
				if sel.c() == "Handle" and sel.adjacent_point not in Globl.currently_selected_flat:
					var adjp = sel.adjacent_point	
					if len(adjp.adjacent_handles()) == 2:
						adjp.auto_handles = false
					sel.along_handle_line -= (moving_selected.y) * .1
					if Input.is_key_pressed(KEY_SHIFT):
						var step_size = 15
						var cur_angle = int(rad_to_deg(adjp.get_handle_line(sel).angle()))
						cur_angle += int(sign(moving_selected.x)) * (step_size + 5*int(sign(moving_selected.x))*sign(cur_angle))
						var closest_whole_angle = cur_angle - ((cur_angle) % step_size)
						adjp.set_handle_line(sel,Vector2.from_angle(deg_to_rad(closest_whole_angle)))
					else:
						adjp.set_handle_line(sel,adjp.get_handle_line(sel).rotated((moving_selected.x/movement_amount) * .04))
					mended_point_list.append(sel.adjacent_point)
				if sel.c() == "Point" and len(sel.adjacent_handles()) > 0 and sel not in auto_adjusted_list:
					ms = sel.myShape()
					auto_adjusted_list = ms.points
					sel.align_handles()
			# if len(auto_adjusted_list) > 0:	
			# 	ms.auto_adjust_all_handles()
			for s in shapes.shapes:
				for p in s.points:
					p.align_handles()
		else:
			marker_pos += moving_selected
			$Cursor.scale = Vector2(.7,.3)
		# for p in mended_point_list:
		# 	p.align_handles()
		for s in shapes.shapes:
			s.make_clockwise()
	marker_pos = check_borders(marker_pos)

func hi_point_adding(delta: float): 
	if Input.is_action_just_pressed("insert_point"):
		for sel in Globl.currently_selected_dict:
			if sel.c() == "Segment":
				uts()
				$Cursor.scale = Vector2(.8,.8)
				var their_shape = sel.myShape
				
				their_shape.add_point(marker_pos,sel)
				manager_node.PlaySound("Laptop_Keystroke_82.wav", 0.2, 1.3, 1.8)

	if Input.is_action_just_pressed("snap_selected"):
		snap_selected_pos()
		uts()

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
		if grid_modifier < 6:
			grid_modifier *= 2
			snap_marker_pos()
	if Input.is_action_just_pressed("decrease_grid_modifier"):
		if grid_modifier > 1:
			grid_modifier /= 2
			snap_marker_pos()
		# else:
		# 	grid_modifier = 1.0 / grid_size
	if Input.is_action_just_pressed("switch_segment_style"):
		for sel: Object in Globl.currently_selected_dict:
			if sel.c() == "Segment":
				sel.switch_segment_type()

	if Input.is_action_just_pressed("switch_point_style"):
		for sel: Object in Globl.currently_selected_dict:
			if sel.c() == "Point":
				sel.switch_point_type()


	if Input.is_action_just_pressed("add_new_point"):
		uts()
		$Cursor.scale = Vector2(.8,.8)
		shape.add_point(marker_pos)
		manager_node.PlaySound("Laptop_Keystroke_82.wav", 0.2, 1.3, 1.8)

	if Input.is_action_just_pressed("finish_shape"):
		uts()
		shapes.add_shape(shape)
		shape = Shape.new()
		manager_node.PlaySound("camera.wav", 0.4, 1.3, 1.8)

func snap_marker_pos():
	var tot: int = grid_modifier * grid_size
	marker_pos.x = int(marker_pos.x / (tot)) * tot
	marker_pos.y = int(marker_pos.y / (tot)) * tot

func snap_selected_pos():
	var tot: int = grid_modifier * grid_size
	for sel in Globl.currently_selected_flat:
		var p = sel.getPos()
		sel.setPos(Vector2(int(p.x/tot) * tot, int(p.y/tot) * tot) - p)

func save_state() -> Dictionary:
	var state: Dictionary = {}
	var props = self.get_property_list()
	for prop in props:
		var nme = prop["name"] 
		if nme == "shape" or nme == "shapes":
			state[nme] = self.get(nme).dup()
		else:
			state[nme] = self.get(nme)
		# elif nme == "Array" or nme == "Dictionary":
	return state

func load_state(state: Dictionary):
	for prop in state:
		self.set(prop, state[prop])
	shapes.set_selection_dicts()
		
func uts():
	# return
	if can_undo_again:
		$UndoRedo.add_to_undo_stack(save_state())
		can_undo_again = false
	if undo_timer.is_stopped():
		undo_timer.start()
	
# func reset_flat_select_dict():	
# 	currently_selected_flat = get_flattened_selection()

# func get_flattened_selection() -> Dictionary:
# 	var d: Dictionary = {}
# 	for sel in currently_selected_dict:
# 		var tpe: String = sel.c()
# 		if tpe == "Handle":
# 			d[sel] = null
# 		elif tpe == "Point": 
# 			for h in sel.adjacent_handles():
# 				d[h] = null
# 			d[sel] = null
# 		elif tpe == "Segment":
# 			d[sel.inPoint] = null
# 			d[sel.outPoint] = null
# 			for h in sel.inPoint.adjacent_handles():
# 				d[h] = null
# 			for h in sel.outPoint.adjacent_handles():
# 				d[h] = null
# 		elif tpe == "Shape":
# 			for p in sel.points:
# 				for h in p.adjacent_handles():
# 					d[h] = null
# 				d[p] = null
# 	return d

func create_random_shape():
	var bord_x = Vector2(300,1500)
	var bord_y = Vector2(300,1500)
	var variation = 800
	var cent: Vector2 = Vector2(randi_range(bord_x.x, bord_x.y), randi_range(bord_y.x,bord_y.y))
	var s: Shape = Shape.new()
	for i in range(randi_range(3,10)):
		s.add_point(cent + Globl.rand_vector2(variation))
	for seg in s.segments:
		if randf() > .8:
			seg.switch_segment_type()
			for h in seg.handles:
				h.setPos(Globl.rand_vector2(200))
	for p in s.points:
		if randf() > .8:
			p.switch_point_type()
	s.close()
	shapes.add_shape(s)

func do_undo_redo():
	can_undo_again = true	

func mt_timeout():
	can_move_again = true

func on_border() -> bool:
	if marker_pos.x == 384 or marker_pos.x == 1152 or marker_pos.y == 512 or marker_pos.y == 1280:
		return true
	else:
		return false
	# if marker_pos.x == 

func create_merged_shape(realShape: Shape) -> Shape:
	var to_merge = realShape.dup()
	return to_merge

func flattenShape(shape: Shape) -> Array:
	var flatShape = []
	for seg in shape.segments:
		var fseg = FlatSegment.new()
		fseg.initFromPoints(seg.inPoint, seg.outPoint, seg.handles)
		flatShape.append(fseg)
	return flatShape

func flatShapeToPoints(fs: Array) -> Array[float]:
	var outAr: Array[float] = []
	for flatseg: FlatSegment in fs:
		var p = flatseg.pointPositionsFlat()
		for ppp in p:
			outAr.append(ppp)
	return outAr

func flatShapeToString(fs: Array) -> String:
	var shape_str: String = ""
	for s in fs:
		if shape_str.length() == 0:
			shape_str += "M {0} {1} ".format([s.inPoint.x, s.inPoint.y])
		shape_str += s.toSVG()
	shape_str += 'Z '
	return shape_str

func merge_flat_shapes(flatShapeA: Array, flatShapeB: Array) -> Array:
	var inters = $Player.find_intersections(flatShapeToPoints(flatShapeA), flatShapeToPoints(flatShapeB))
	# print("wat er in inters zit:")
	# for i in inters:
	# 	print(i)
	# print(len(inters))
	var outAr = []
	var segAr = []

	var cc = 0
	print(len(inters))
	for i in range(0,len(inters)): 
		if inters[i] == -9999.0:
			print("\ngap\n")
			cc = 0
			outAr.append(segAr.duplicate())
			segAr = []
			continue	
		else:
			# print(inters[i])
			if cc == 7:
				cc = 0
				var j = i - 7
				var p1 = Vector2(inters[j], inters[j+1])
				var p2 = Vector2(inters[j+2], inters[j+3])
				var p3 = Vector2(inters[j+4], inters[j+5])
				var p4 = Vector2(inters[j+6], inters[j+7])
				var vs = FlatSegment.new([p1,p2,p3,p4])
				segAr.append(vs)
			else:
				cc += 1
		# vs.print()
	# print(outAr)
	# print(len(outAr[0]))
	# print(len(outAr[1]))
	# for s in outAr[1]:
	# 	s.reverse_segment()
	# outAr[1].reverse()
	return outAr

func create_ghost_shape_flat(realShape: Array) -> Array:
	var ROUNDED = false
	var amount_in_len = 20
	var handle_dist = 10
	var trimmedSegList = []
	for seg in realShape:
		if length_cubic(seg) < 2 * amount_in_len:
			continue
		var amount = (amount_in_len / length_cubic(seg))
		trimmedSegList.append(trimmed_tangent_and_pos(seg,amount, 1.0 - amount))
	if len(trimmedSegList) == 0:
		return realShape
	var ghostFlat = []
	var c = 0
	for s in trimmedSegList:

		var tr1 = s[0]
		var tan1 = s[2]
		var tr2
		var tan2
		if c < len(trimmedSegList) - 1:
			tr2 = trimmedSegList[c+1][0]
			tan2 = trimmedSegList[c+1][1]
		else:
			tr2 = trimmedSegList[0][0]
			tan2 = trimmedSegList[0][1]
		if !ROUNDED:
			var v1 = Vector2(tr1[0], tr1[1])
			var v2 = Vector2(tr1[6], tr1[7])
			var newFlatSeg = FlatSegment.new([v1,Vector2(tr1[2], tr1[3]),Vector2(tr1[4], tr1[5]),v2])
			ghostFlat.append(newFlatSeg)
			var v3 = Vector2(tr2[0], tr2[1])
			var newFlatSeg2 = FlatSegment.new([v2,v2,v3,v3])
			newFlatSeg2.addHandles()
			ghostFlat.append(newFlatSeg2)
		else:
			var v1 = Vector2(tr1[0], tr1[1])
			var v2 = Vector2(tr1[6], tr1[7])
			var newFlatSeg = FlatSegment.new([v1,Vector2(tr1[2], tr1[3]),Vector2(tr1[4], tr1[5]),v2])
			ghostFlat.append(newFlatSeg)
			var v3 = Vector2(tr2[0], tr2[1])
			var newPos1 = Vector2(tr1[6],tr1[7]) + tan1.normalized() * handle_dist
			var newPos2 = Vector2(tr2[0], tr2[1]) - tan2.normalized() * handle_dist
			var newFlatSeg2 = FlatSegment.new([v2,newPos1,newPos2,v3])
			ghostFlat.append(newFlatSeg2)
		c+=1
	return ghostFlat

func create_ghost_shape2(realShape: Shape) -> String:
	var ROUNDED = false
	var amount_in_len = 80
	var handle_dist = 20
	var trimmedSegList = []
	for seg in realShape.segments:
		if seg.length() < 2 * amount_in_len:
			continue
		var amount = (amount_in_len / length_cubic(seg))
		trimmedSegList.append(trimmed_tangent_and_pos(seg,amount, 1.0 - amount))
	if len(trimmedSegList) == 0:
		return realShape.shape_string()
	var c = 0
	var svgstring = "M "
	var trimmedSeg1 = trimmedSegList[0][0]

	svgstring += (str(trimmedSeg1[0]) + " " + str(trimmedSeg1[1]) + " ")
	for s in trimmedSegList:
		svgstring += "C "

		var tr1 = s[0]
		var tan1 = s[2]
		var tr2
		var tan2
		if c < len(trimmedSegList) - 1:
			tr2 = trimmedSegList[c+1][0]
			tan2 = trimmedSegList[c+1][1]
		else:
			tr2 = trimmedSegList[0][0]
			tan2 = trimmedSegList[0][1]
		svgstring += (str(tr1[2]) + " " + str(tr1[3]) + ",")
		svgstring += (str(tr1[4]) + " " + str(tr1[5]) + ",")
		svgstring += (str(tr1[6]) + " " + str(tr1[7]) + " ")
		if !ROUNDED:
			svgstring += ("L " + str(tr2[0]) + " " + str(tr2[1]) + " ")
		else:
			svgstring += "C "
			var newPos1 = Vector2(tr1[6],tr1[7]) + tan1.normalized() * handle_dist
			var newPos2 = Vector2(tr2[0], tr2[1]) - tan2.normalized() * handle_dist
			svgstring += (str(newPos1[0]) + " " + str(newPos1[1]) + ",")
			svgstring += (str(newPos2[0]) + " " + str(newPos2[1]) + ",")
			svgstring += (str(tr2[0]) + " " + str(tr2[1]) + ",")
		c+=1
	svgstring += " Z\n"
	# return realShape.to_string()
	return svgstring

func trimmed_tangent_and_pos(seg, trim1: float, trim2: float):
	var pps = seg.pointPositionsFlat()
	var tan_pos = $Player.trimmed_tangent_parametric(pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7], trim1, trim2)
	var trimmed: Array = tan_pos.slice(0,8)
	var tang_vector_start = Vector2(tan_pos[8+0], tan_pos[8+1])
	var tang_vector_end = Vector2(tan_pos[8+2], tan_pos[8+3])
	# var trimmed: Array = [0,1,2,3,4,5,6,7]
	# var tang_vector_start = Vector2(tan_pos[0], tan_pos[1])
	# var tang_vector_end = Vector2(tan_pos[0], tan_pos[1])
	return [trimmed, tang_vector_start, tang_vector_end]

func tangent_and_pos(seg: Segment, t: float, par: bool = true):
	var pps = seg.pointPositionsFlat()
	var where_along: Array
	if par:
		where_along = $Player.point_along_cubic_parametric(t,pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])
	else:
		where_along = $Player.point_along_cubic_euclidean(t,pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])
	var wa_vec: Vector2 = Vector2(where_along[0], where_along[1])
	var tang: Array = $Player.tangent_parametric(t,pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])
	var tang_vector = Vector2(tang[0], tang[1])
	return [tang_vector, wa_vec]

func length_cubic(seg) -> float:
	var pps = seg.pointPositionsFlat()
	return $Player.length_cubic(pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7])

func trim(seg, t1,t2, parametric: bool):
	var pps = seg.pointPositionsFlat()
	var trimmed: Array =  $Player.bezier_trimmed(pps[0],pps[1],pps[2],pps[3],pps[4],pps[5],pps[6],pps[7], t1,t2, parametric)
	return trimmed
	# return pps
	# seg.inPoint.pos.x = trimmed[0]
	# seg.inPoint.pos.y = trimmed[1]
	# if len(seg.handles) == 2:
	# 	if seg.handles[0].inHandle:
	# 		seg.handles[0].pos.x = trimmed[2]
	# 		seg.handles[0].pos.y = trimmed[3]
	# 		seg.handles[1].pos.x = trimmed[4]
	# 		seg.handles[1].pos.y = trimmed[5]
	# 	else:
	# 		seg.handles[0].pos.x = trimmed[4]
	# 		seg.handles[0].pos.y = trimmed[5]
	# 		seg.handles[1].pos.x = trimmed[2]
	# 		seg.handles[1].pos.y = trimmed[3]
	# seg.outPoint.pos.x = trimmed[6]
	# seg.outPoint.pos.y = trimmed[7]

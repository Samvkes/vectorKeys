extends Node


var select_label_pool: Array[String]
var currently_selected: Array
var possible_selections_dict: Dictionary = {}
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var file = FileAccess.open("res://pool.txt", FileAccess.READ)
	var content = file.get_as_text()
	select_label_pool.assign(content.split("\n"))
	for i in range(0, len(select_label_pool)):
		select_label_pool[i] = select_label_pool[i].strip_escapes()


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass

func add_to_possible_selections(to_add) -> String:
	var label = select_label_pool.pop_front()
	possible_selections_dict[label] = to_add
	return label
	# print("possible selections dict: " + str(Globl.possible_selections_dict))

func project_point_on_line(to_project: Vector2, line_start: Vector2, line_end: Vector2) -> Vector2i: 
	# vector that runs along line
	print("t_project, line_start, line_end = " + str(to_project) + "    " + str(line_start) + "     " + str(line_end))
	var d: Vector2 = line_end - line_start
	print("d = " + str(d))
	# p with A as origin
	var v: Vector2 = to_project - line_start
	print("v = " + str(v))

	# amount of 'd-vectors' to march along d from a to find projection
	var t = v.dot(d) / (d.length() * d.length())
	print("t= " + str(t))

	var q = line_start + (t * d)
	print("q= " + str(q))
	return q
	# var AD: Vector2 = AB * AB.dot(AC) / AB.dot(AB)
	# var D: Vector2i = line_start + AD
	# return D
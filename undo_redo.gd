extends Node

const MAX_STACK_SIZE = 100
var _undo_stack: Array = []
var _redo_stack: Array = []
var _current_state: Dictionary = {}

func _ready() -> void:
    pass
    
func _process(delta: float) -> void:
    pass

func undo():
    if len(_undo_stack) > 0:
        print("undoing")
        if _current_state == {}:
            _current_state = get_parent().save_state()
        _redo_stack.append(_current_state)
        if len(_redo_stack) > MAX_STACK_SIZE:
            _redo_stack.pop_front()
        var state = _undo_stack.pop_back()
        get_parent().load_state(state)
        _current_state = state
    else:
        print("undo stack is empty")

func redo():
    if len(_redo_stack) > 0:
        print("redoing")
        _undo_stack.append(_current_state)
        if len(_undo_stack) > MAX_STACK_SIZE:
            _undo_stack.pop_front()
        var state = _redo_stack.pop_back()
        get_parent().load_state(state)
        _current_state = state
    else:
        print("redo stack is empty")

func add_to_undo_stack(state: Dictionary):
    _undo_stack.append(state)
    _redo_stack.clear()
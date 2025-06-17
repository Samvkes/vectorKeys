use bezier_rs::TValueType;
use godot::prelude::*;
use godot::classes::Sprite2D;
use godot::classes::ISprite2D;
use bezier_rs::Bezier;
use bezier_rs::TValue;

struct MyExtension;

#[gdextension]
unsafe impl ExtensionLibrary for MyExtension {}


#[derive(GodotClass)]
#[class(base=Sprite2D)]
struct Player {
    speed: f64,
    angular_speed: f64,

    base: Base<Sprite2D>
}

#[godot_api]
impl ISprite2D for Player {
    fn init(base: Base<Sprite2D>) -> Self {
        godot_print!("Hello, world!"); // Prints to the Godot console
        
        Self {
            speed: 400.0,
            angular_speed: std::f64::consts::PI,
            base,
        }
    }
    fn physics_process(&mut self, delta: f64) {
        // In GDScript, this would be: 
        // rotation += angular_speed * delta
        
        let radians = (self.angular_speed * delta) as f32;
        self.base_mut().rotate(radians);
        // The 'rotate' method requires a f32, 
        // therefore we convert 'self.angular_speed * delta' which is a f64 to a f32
        let rotation = self.base().get_rotation();
        let velocity = Vector2::UP.rotated(rotation) * self.speed as f32;
        self.base_mut().translate(velocity * delta as f32);
        
        // or verbose: 
        // let this = self.base_mut();
        // this.set_position(
        //     this.position() + velocity * delta as f32
        // );
    }
}

#[godot_api]
impl Player {
    #[func]
    fn increase_speed(&mut self, amount: f64) {
        self.speed += amount;
        self.base_mut().emit_signal("speed_increased", &[]);
    }

    #[func]
    fn point_along_cubic_euclidean(&mut self, amount: f64, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.evaluate(TValue::Euclidean(amount));
        return [a[0], a[1]];
    }

    #[func]
    fn point_along_cubic_parametric(&mut self, amount: f64, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.evaluate(TValue::Parametric(amount));
        return [a[0], a[1]];
    }

    #[func]
    fn tangent_parametric(&mut self, amount: f64, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> [f64; 2]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let a = bez.tangent(TValue::Parametric(amount));
        return [a[0], a[1]];
    }

    #[signal]
    fn speed_increased();
}

use bezier_rs::TValueType;
use glam::DVec2;
use godot::classes::rendering_device::DeviceType;
use godot::global::print;
use godot::global::print_verbose;
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
    fn find_intersections(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64, x5: f64,y5: f64,x6: f64,y6: f64,x7: f64,y7: f64,x8: f64,y8: f64) -> Array<f64>  {
        let bez1 = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let bez2 = Bezier::from_cubic_coordinates(x5, y5, x6, y6, x7, y7, x8, y8);
        let t_vals = bez1.intersections(&bez2, Some(0.5), Some(0.05));
        let mut a: Array<f64> = Array::new();
        return a;
    }

    #[func]
    fn length_cubic(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64) -> f64  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        return bez.length(Some(1000));
    }

    #[func]
    fn bezier_trimmed(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64, t1: f64, t2:f64, param:bool) -> [f64; 8]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let trimmed: Bezier;
        if param{
            trimmed = bez.trim(TValue::Parametric(t1),TValue::Parametric(t2));
        }
        else {
            trimmed = bez.trim(TValue::Euclidean(t1),TValue::Euclidean(t2));
        }
        let h1: DVec2 = Option::expect(trimmed.handle_start(), "no handles?");
        let h2: DVec2 = Option::expect(trimmed.handle_end(), "no handles?");
        return [trimmed.start().x, trimmed.start().y,h1.x,h1.y,h2.x,h2.y, trimmed.end().x, trimmed.end().y];
        // return bez.length(Some(1000));
    }

    #[func]
    fn trimmed_tangent_parametric(&mut self, x1: f64,y1: f64,x2: f64,y2: f64,x3: f64,y3: f64,x4: f64,y4: f64, t1: f64, t2: f64) -> [f64; 12]  {
        let bez = Bezier::from_cubic_coordinates(x1, y1, x2, y2, x3, y3, x4, y4);
        let trimmed: Bezier;
        trimmed = bez.trim(TValue::Euclidean(t1),TValue::Euclidean(t2));
        // trimmed = bez;

        let tan_start = trimmed.tangent(TValue::Euclidean(0.01));
        let tan_end = trimmed.tangent(TValue::Euclidean(0.99));
        let h1: DVec2 = Option::expect(trimmed.handle_start(), "no handles?");
        let h2: DVec2 = Option::expect(trimmed.handle_end(), "no handles?");
        return [trimmed.start().x, trimmed.start().y,h1.x,h1.y,h2.x,h2.y, trimmed.end().x, trimmed.end().y, tan_start[0], tan_start[1], tan_end[0], tan_end[1]];
        // return [trimmed.start().x, trimmed.start().y]
        // return [a[0], a[1], b[0], b[1]];
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
